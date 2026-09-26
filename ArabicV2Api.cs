using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

public static class ArabicV2Api
{
    static readonly object DataLock=new();
    public static void MapArabicV2(this WebApplication app, string dataDir)
    {
        var listsFile=Path.Combine(dataDir,"daily-lists.json");
        var bookingsFile=Path.Combine(dataDir,"bookings-v2.json");
        var auditFile=Path.Combine(dataDir,"audit-v2.json");
        var doctorsFile=Path.Combine(dataDir,"doctors-v2.json");
        var adminFile=Path.Combine(dataDir,"admin-v2.json");
        var settingsFile=Path.Combine(dataDir,"settings-v2.json");
        var adminTokens=new Dictionary<string,DateTime>();
        var mutationGate=new SemaphoreSlim(1,1);
        bool IsAdmin(HttpRequest r){var t=r.Headers["X-Admin-Token"].ToString();lock(adminTokens){return !string.IsNullOrWhiteSpace(t)&&adminTokens.TryGetValue(t,out var exp)&&exp>DateTime.Now;}}
        Init(listsFile,"[]"); Init(bookingsFile,"[]"); Init(auditFile,"[]");
        Init(doctorsFile,JsonSerializer.Serialize(new[]{"د. أحمد","د. محمد"}));
        Init(adminFile,JsonSerializer.Serialize(new V2AdminConfig{pinHash=HashPin("1234")}));
        Init(settingsFile,JsonSerializer.Serialize(new V2Settings{workingDays=new[]{0,1,2,3,4,6},duplicateNameDays=30,requireMorning=true,requireEvening=true,alertsStartDate=DateTime.Today}));

        app.MapPost("/api/v2/admin/login",async(HttpRequest r)=>{
            var x=await JsonSerializer.DeserializeAsync<V2AdminLogin>(r.Body);var cfg=ReadOne<V2AdminConfig>(adminFile);
            if(x==null||cfg.pinHash!=HashPin(x.pin))return Results.Unauthorized();
            var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));lock(adminTokens)adminTokens[token]=DateTime.Now.AddHours(8);Audit(auditFile,x.actor,"دخول لوحة الأدمن","تم فتح لوحة الأدمن",r);return Results.Ok(new{ok=true,token});
        });
        app.MapPost("/api/v2/admin/change-pin",async(HttpRequest r)=>{
            if(!IsAdmin(r))return Results.Unauthorized();var x=await JsonSerializer.DeserializeAsync<V2AdminPinChange>(r.Body);var cfg=ReadOne<V2AdminConfig>(adminFile);
            if(x==null||cfg.pinHash!=HashPin(x.oldPin)||string.IsNullOrWhiteSpace(x.newPin)||x.newPin.Length<4)return Results.BadRequest();
            cfg.pinHash=HashPin(x.newPin);lock(DataLock)AtomicWrite(adminFile,JsonSerializer.Serialize(cfg,new JsonSerializerOptions{WriteIndented=true}));
            Audit(auditFile,x.actor,"تغيير PIN الأدمن","تم تغيير رمز لوحة الأدمن",r);return Results.Ok();
        });
        app.MapDelete("/api/v2/doctors", (string name,string actor,HttpRequest r)=>{
            if(!IsAdmin(r))return Results.Unauthorized();
            var a=Read<string>(doctorsFile);a.RemoveAll(x=>x==name);Write(doctorsFile,a);Audit(auditFile,actor,"حذف طبيب",name,r);return Results.Ok();
        });
        app.MapPost("/api/v2/admin/backup",(string actor,HttpRequest r)=>{
            if(!IsAdmin(r))return Results.Unauthorized();
            var stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");var bd=Path.Combine(dataDir,"backups-v2",stamp);Directory.CreateDirectory(bd);
            foreach(var file in new[]{listsFile,bookingsFile,auditFile,doctorsFile,adminFile,settingsFile})if(File.Exists(file))File.Copy(file,Path.Combine(bd,Path.GetFileName(file)),true);
            Audit(auditFile,actor,"نسخة احتياطية",stamp,r);return Results.Ok(new{folder=stamp});
        });

        app.MapGet("/api/v2/settings",()=>Results.Json(ReadOne<V2Settings>(settingsFile)));
        app.MapPost("/api/v2/settings",async(HttpRequest r)=>{
            if(!IsAdmin(r))return Results.Unauthorized();var x=await JsonSerializer.DeserializeAsync<V2SettingsUpdate>(r.Body);if(x==null)return Results.BadRequest();
            var s=x.settings??new V2Settings();s.duplicateNameDays=Math.Clamp(s.duplicateNameDays,1,365);s.workingDays=(s.workingDays??[]).Distinct().Where(d=>d>=0&&d<=6).OrderBy(d=>d).ToArray();if(s.workingDays.Length==0)return Results.BadRequest(new{error="working_days_required"});if(s.alertsStartDate==default)s.alertsStartDate=DateTime.Today;s.alertsStartDate=s.alertsStartDate.Date;if(s.alertsStartDate>DateTime.Today)s.alertsStartDate=DateTime.Today;
            lock(DataLock)AtomicWrite(settingsFile,JsonSerializer.Serialize(s,new JsonSerializerOptions{WriteIndented=true}));
            Audit(auditFile,x.actor,"تعديل إعدادات التشغيل",$"أيام العمل: {string.Join(",",s.workingDays)} - فترة التكرار: {s.duplicateNameDays} يوم",r);return Results.Ok(s);
        });

        app.MapGet("/api/v2/doctors",()=>Results.Json(Read<string>(doctorsFile)));
        app.MapPost("/api/v2/doctors",async(HttpRequest r)=>{
            if(!IsAdmin(r))return Results.Unauthorized();var x=await JsonSerializer.DeserializeAsync<V2DoctorInput>(r.Body);
            if(x==null||string.IsNullOrWhiteSpace(x.name)) return Results.BadRequest();
            var a=Read<string>(doctorsFile); if(!a.Contains(x.name.Trim()))a.Add(x.name.Trim()); Write(doctorsFile,a);
            Audit(auditFile,x.actor,"إضافة طبيب",x.name,r); return Results.Ok();
        });

        app.MapGet("/api/v2/lists",()=>Results.Json(Read<V2DailyList>(listsFile).OrderByDescending(x=>x.date)));
        app.MapPost("/api/v2/lists",async(HttpRequest r)=>{
            await mutationGate.WaitAsync();try{
            var x=await JsonSerializer.DeserializeAsync<V2DailyList>(r.Body);
            if(x==null||string.IsNullOrWhiteSpace(x.doctor)||string.IsNullOrWhiteSpace(x.shift))return Results.BadRequest();
            var a=Read<V2DailyList>(listsFile);
            if(a.Any(z=>z.date.Date==x.date.Date&&z.doctor==x.doctor&&z.shift==x.shift))return Results.Conflict(new{error="list_exists"});
            x.id=Guid.NewGuid().ToString("N")[..8].ToUpper(); x.state="مفتوحة"; x.createdAt=DateTime.Now; x.updatedAt=DateTime.Now;
            a.Add(x);Write(listsFile,a);Audit(auditFile,x.actor,"إنشاء قائمة",$"{x.date:yyyy-MM-dd} - {x.doctor} - {x.shift}",r);return Results.Ok(x);
            }finally{mutationGate.Release();}
        });
        app.MapPut("/api/v2/lists/{id}/state",async(string id,HttpRequest r)=>{
            await mutationGate.WaitAsync();try{
            var sc=await JsonSerializer.DeserializeAsync<V2StateChange>(r.Body);var a=Read<V2DailyList>(listsFile);var x=a.FirstOrDefault(z=>z.id==id);
            if(sc==null||x==null)return Results.NotFound();if(!new[]{"مفتوحة","مقفلة","مكتملة"}.Contains(sc.state))return Results.BadRequest(new{error="invalid_state"});
            var old=x.state;x.state=sc.state;x.updatedAt=DateTime.Now;x.modifiedBy=sc.actor;Write(listsFile,a);
            Audit(auditFile,sc.actor,"تغيير حالة القائمة",$"{x.doctor} - {x.shift}: {old} ← {x.state}",r);return Results.Ok(x);
            }finally{mutationGate.Release();}
        });
        app.MapPut("/api/v2/lists/{id}",async(string id,HttpRequest r)=>{
            await mutationGate.WaitAsync();try{
            var n=await JsonSerializer.DeserializeAsync<V2ListEdit>(r.Body);var a=Read<V2DailyList>(listsFile);var x=a.FirstOrDefault(z=>z.id==id);
            if(n==null||x==null)return Results.NotFound();if(string.IsNullOrWhiteSpace(n.doctor)||!new[]{"صباحي","مسائي"}.Contains(n.shift))return Results.BadRequest(new{error="invalid_data"});
            if(a.Any(z=>z.id!=id&&z.date.Date==n.date.Date&&z.doctor==n.doctor&&z.shift==n.shift))return Results.Conflict(new{error="list_exists"});
            var old=$"{x.date:yyyy-MM-dd} - {x.doctor} - {x.shift}";x.date=n.date.Date;x.doctor=n.doctor.Trim();x.shift=n.shift;x.updatedAt=DateTime.Now;x.modifiedBy=n.actor;Write(listsFile,a);
            var bs=Read<V2Booking>(bookingsFile);foreach(var b in bs.Where(z=>z.listId==id)){b.date=x.date;b.doctor=x.doctor;b.shift=x.shift;b.updatedAt=DateTime.Now;}Write(bookingsFile,bs);
            Audit(auditFile,n.actor,"تعديل قائمة",$"{old} ← {x.date:yyyy-MM-dd} - {x.doctor} - {x.shift}",r);return Results.Ok(x);
            }finally{mutationGate.Release();}
        });

        app.MapGet("/api/v2/bookings",(bool includeDeleted=false)=>Results.Json(Read<V2Booking>(bookingsFile).Where(x=>includeDeleted||!x.isDeleted).OrderByDescending(x=>x.createdAt)));
        app.MapPost("/api/v2/bookings",async(HttpRequest r)=>{
            await mutationGate.WaitAsync();try{
            var x=await JsonSerializer.DeserializeAsync<V2Booking>(r.Body);if(x==null)return Results.BadRequest();
            if(string.IsNullOrWhiteSpace(x.patientName)||string.IsNullOrWhiteSpace(x.exam)||!System.Text.RegularExpressions.Regex.IsMatch(x.phone??"","^\\d{11}$"))return Results.BadRequest(new{error="invalid_data"});
            var lists=Read<V2DailyList>(listsFile);var list=lists.FirstOrDefault(z=>z.id==x.listId);if(list==null)return Results.BadRequest(new{error="list_missing"});
            if(list.state!="مفتوحة")return Results.Conflict(new{error="list_locked"});
            var a=Read<V2Booking>(bookingsFile);x.id=Guid.NewGuid().ToString("N")[..8].ToUpper();
            var samePhone=a.Where(z=>!z.isDeleted&&z.phone==x.phone).OrderByDescending(z=>z.createdAt).FirstOrDefault();
            if(string.IsNullOrWhiteSpace(x.patientId)&&samePhone!=null)x.patientId=samePhone.patientId;
            if(string.IsNullOrWhiteSpace(x.patientId))x.patientId=NextPatientId(a);
            var idOwner=a.FirstOrDefault(z=>!z.isDeleted&&z.patientId.Equals(x.patientId,StringComparison.OrdinalIgnoreCase));
            if(idOwner!=null&&idOwner.phone!=x.phone)return Results.Conflict(new{error="patient_id_conflict",patientId=x.patientId,existingName=idOwner.patientName});
            x.date=list.date;x.doctor=list.doctor;x.shift=list.shift;x.createdAt=x.updatedAt=DateTime.Now;
            a.Add(x);Write(bookingsFile,a);Audit(auditFile,x.actor,"إضافة حجز",$"{x.patientId} - {x.patientName} - {x.doctor} - {x.shift}",r);return Results.Ok(x);
            }finally{mutationGate.Release();}
        });
        app.MapPut("/api/v2/bookings/{id}",async(string id,HttpRequest r)=>{
            await mutationGate.WaitAsync();try{
            var n=await JsonSerializer.DeserializeAsync<V2Booking>(r.Body);var a=Read<V2Booking>(bookingsFile);var i=a.FindIndex(z=>z.id==id);
            if(n==null||i<0)return Results.NotFound();var sourceList=Read<V2DailyList>(listsFile).FirstOrDefault(z=>z.id==a[i].listId);if(sourceList?.state=="مكتملة")return Results.Conflict(new{error="list_completed"});if(!System.Text.RegularExpressions.Regex.IsMatch(n.phone??"","^\\d{11}$"))return Results.BadRequest(new{error="invalid_phone"});
            var old=a[i];var changes=new List<string>();
            if(old.patientName!=n.patientName)changes.Add($"الاسم: {old.patientName} ← {n.patientName}");
            if(old.phone!=n.phone)changes.Add($"الهاتف: {old.phone} ← {n.phone}");
            if(old.exam!=n.exam)changes.Add($"الفحص: {old.exam} ← {n.exam}");
            if(old.contractType!=n.contractType)changes.Add($"التعاقد: {old.contractType} ← {n.contractType}");
            if(old.notes!=n.notes)changes.Add("تم تعديل الملاحظات");
            n.id=id;n.patientId=string.IsNullOrWhiteSpace(n.patientId)?old.patientId:n.patientId;n.createdAt=old.createdAt;n.status=old.status;n.isDeleted=old.isDeleted;n.deletedAt=old.deletedAt;n.deletedBy=old.deletedBy;n.updatedAt=DateTime.Now;a[i]=n;Write(bookingsFile,a);
            Audit(auditFile,n.actor,"تعديل بيانات",$"{n.patientId} - {n.patientName}: {(changes.Count>0?string.Join(" | ",changes):"بدون تغيير")}",r);return Results.Ok(n);
            }finally{mutationGate.Release();}
        });
        app.MapDelete("/api/v2/bookings/{id}",async(string id,HttpRequest r)=>{
            await mutationGate.WaitAsync();try{
            var x=await JsonSerializer.DeserializeAsync<V2DeleteBooking>(r.Body);var a=Read<V2Booking>(bookingsFile);var b=a.FirstOrDefault(z=>z.id==id);
            if(x==null||b==null)return Results.NotFound();b.isDeleted=true;b.deletedAt=DateTime.Now;b.deletedBy=x.actor;b.updatedAt=DateTime.Now;Write(bookingsFile,a);
            Audit(auditFile,x.actor,"حذف حالة",$"{b.patientId} - {b.patientName} - السبب: {x.reason}",r);return Results.Ok();
            }finally{mutationGate.Release();}
        });
        app.MapPut("/api/v2/bookings/{id}/status",async(string id,HttpRequest r)=>{
            await mutationGate.WaitAsync();try{
            var x=await JsonSerializer.DeserializeAsync<V2BookingAction>(r.Body);var a=Read<V2Booking>(bookingsFile);var b=a.FirstOrDefault(z=>z.id==id);
            if(x==null||b==null)return Results.NotFound();if(!new[]{"محجوز","تم الحضور","لم يحضر","ملغي"}.Contains(x.status))return Results.BadRequest(new{error="invalid_status"});var source=Read<V2DailyList>(listsFile).FirstOrDefault(z=>z.id==b.listId);if(source?.state=="مكتملة")return Results.Conflict(new{error="list_completed"});var old=b.status;b.status=x.status;b.updatedAt=DateTime.Now;b.actor=x.actor;Write(bookingsFile,a);
            Audit(auditFile,x.actor,"تغيير حالة حجز",$"{b.patientId} - {b.patientName}: {old} ← {b.status}",r);return Results.Ok(b);
            }finally{mutationGate.Release();}
        });
        app.MapPut("/api/v2/bookings/{id}/move",async(string id,HttpRequest r)=>{
            await mutationGate.WaitAsync();try{
            var x=await JsonSerializer.DeserializeAsync<V2BookingMove>(r.Body);var a=Read<V2Booking>(bookingsFile);var b=a.FirstOrDefault(z=>z.id==id);
            var lists=Read<V2DailyList>(listsFile);var target=x==null?null:lists.FirstOrDefault(z=>z.id==x.listId);
            if(x==null||b==null||target==null)return Results.NotFound();var source=lists.FirstOrDefault(z=>z.id==b.listId);if(source?.state=="مكتملة")return Results.Conflict(new{error="source_completed"});if(target.state!="مفتوحة")return Results.Conflict(new{error="target_locked"});
            var old=$"{b.date:yyyy-MM-dd} - {b.doctor} - {b.shift}";b.listId=target.id;b.date=target.date;b.doctor=target.doctor;b.shift=target.shift;b.updatedAt=DateTime.Now;b.actor=x.actor;Write(bookingsFile,a);
            Audit(auditFile,x.actor,"نقل حجز",$"{b.patientId} - {b.patientName}: {old} ← {b.date:yyyy-MM-dd} - {b.doctor} - {b.shift}",r);return Results.Ok(b);
            }finally{mutationGate.Release();}
        });
        app.MapGet("/api/v2/patients/search",(string q)=>{
            q=(q??"").Trim().ToLowerInvariant();if(q.Length<2)return Results.Json(Array.Empty<V2Booking>());
            var a=Read<V2Booking>(bookingsFile).Where(x=>!x.isDeleted&&((x.patientName??"").ToLowerInvariant().Contains(q)||(x.phone??"").Contains(q)||(x.patientId??"").ToLowerInvariant().Contains(q))).OrderByDescending(x=>x.date).Take(50);
            return Results.Json(a);
        });
        app.MapGet("/api/v2/patients/by-phone",(string phone)=>{
            var b=Read<V2Booking>(bookingsFile).Where(x=>!x.isDeleted&&x.phone==phone).OrderByDescending(x=>x.createdAt).FirstOrDefault();
            return b==null?Results.NotFound():Results.Ok(new{b.patientId,b.patientName,b.phone,lastDate=b.date,lastDoctor=b.doctor,lastShift=b.shift,lastExam=b.exam});
        });
        app.MapGet("/api/v2/audit",(HttpRequest r)=>IsAdmin(r)?Results.Json(Read<V2Audit>(auditFile).OrderByDescending(x=>x.time).Take(1000)):Results.Unauthorized());
        app.MapGet("/api/v2/admin/summary",(HttpRequest r)=>{if(!IsAdmin(r))return Results.Unauthorized();return Results.Ok(new{
            doctors=Read<string>(doctorsFile).Count,
            lists=Read<V2DailyList>(listsFile).Count,
            bookings=Read<V2Booking>(bookingsFile).Count,
            dataPath=dataDir,
            server=Environment.MachineName
        });});
        app.MapGet("/api/v2/alerts",()=>{
            var lists=Read<V2DailyList>(listsFile);var s=ReadOne<V2Settings>(settingsFile);var today=DateTime.Today;var alerts=new List<V2Alert>();
            var start=s.alertsStartDate==default?(lists.Count>0?lists.Min(x=>x.date.Date):today):s.alertsStartDate.Date;if(start>today)start=today;
            for(var d=start;d<=today;d=d.AddDays(1)){
                if(!s.workingDays.Contains((int)d.DayOfWeek))continue;
                var day=lists.Where(x=>x.date.Date==d).ToList();var morning=day.Any(x=>x.shift=="صباحي");var evening=day.Any(x=>x.shift=="مسائي");
                if(day.Count==0)alerts.Add(new V2Alert{type=d==today?"today_no_lists":"old_no_lists",date=d,severity=d==today?"danger":"warning",message=d==today?"لم يتم إنشاء أي قائمة لليوم حتى الآن":$"لم يتم إنشاء قوائم يوم {d:dd/MM/yyyy}"});
                if(s.requireMorning&&!morning&&day.Count>0)alerts.Add(new V2Alert{type="missing_morning",date=d,severity="warning",message=d==today?"لم يتم إنشاء القائمة الصباحية لليوم":$"لم يتم إنشاء القائمة الصباحية بتاريخ {d:dd/MM/yyyy}"});
                if(s.requireEvening&&!evening&&day.Count>0)alerts.Add(new V2Alert{type="missing_evening",date=d,severity="warning",message=d==today?"لم يتم إنشاء القائمة المسائية لليوم":$"لم يتم إنشاء القائمة المسائية بتاريخ {d:dd/MM/yyyy}"});
                if(d<today&&day.Any(x=>x.state!="مكتملة"))alerts.Add(new V2Alert{type="old_incomplete",date=d,severity="danger",message=$"قوائم سابقة غير مكتملة بتاريخ {d:dd/MM/yyyy}"});
            }
            return Results.Json(alerts.OrderByDescending(x=>x.date).ThenBy(x=>x.type));
        });
    }
    static string NextPatientId(List<V2Booking> a){var n=a.Select(x=>int.TryParse((x.patientId??"").Replace("P",""),out var v)?v:0).DefaultIfEmpty(0).Max()+1;return $"P{n:000000}";}
    static void Audit(string f,string actor,string action,string detail,HttpRequest r){lock(DataLock){var a=Read<V2Audit>(f);a.Add(new V2Audit{time=DateTime.Now,actor=string.IsNullOrWhiteSpace(actor)?"غير محدد":actor,action=action,detail=detail,device=r.Headers["User-Agent"].ToString()});Write(f,a);}}
    static string HashPin(string s)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s??"")));
    static T ReadOne<T>(string p) where T:new(){lock(DataLock)return JsonSerializer.Deserialize<T>(File.ReadAllText(p))??new T();}
    static void Init(string p,string v){lock(DataLock){if(!File.Exists(p))File.WriteAllText(p,v);}}
    static List<T> Read<T>(string p){lock(DataLock)return JsonSerializer.Deserialize<List<T>>(File.ReadAllText(p))??[];}
    static void Write<T>(string p,IEnumerable<T> x){lock(DataLock)AtomicWrite(p,JsonSerializer.Serialize(x,new JsonSerializerOptions{WriteIndented=true}));}
    static void AtomicWrite(string p,string content){var tmp=p+".tmp";File.WriteAllText(tmp,content);File.Move(tmp,p,true);}
}
public class V2DailyList{public string id{get;set;}="";public DateTime date{get;set;}public string doctor{get;set;}="";public string shift{get;set;}="صباحي";public string state{get;set;}="مفتوحة";public string actor{get;set;}="";public string modifiedBy{get;set;}="";public DateTime createdAt{get;set;}public DateTime updatedAt{get;set;}}
public class V2Booking{public string id{get;set;}="";public string listId{get;set;}="";public string patientId{get;set;}="";public string patientName{get;set;}="";public string phone{get;set;}="";public string contractType{get;set;}="نقدي";public string exam{get;set;}="";public string doctor{get;set;}="";public string shift{get;set;}="";public DateTime date{get;set;}public string notes{get;set;}="";public string status{get;set;}="محجوز";public bool isDeleted{get;set;}=false;public DateTime? deletedAt{get;set;}public string deletedBy{get;set;}="";public string actor{get;set;}="";public DateTime createdAt{get;set;}public DateTime updatedAt{get;set;}}
public class V2Audit{public DateTime time{get;set;}public string actor{get;set;}="";public string action{get;set;}="";public string detail{get;set;}="";public string device{get;set;}="";}
public class V2StateChange{public string state{get;set;}="";public string actor{get;set;}="";}
public class V2DoctorInput{public string name{get;set;}="";public string actor{get;set;}="";}

public class V2Alert{public string type{get;set;}="";public DateTime date{get;set;}public string severity{get;set;}="warning";public string message{get;set;}="";}

public class V2AdminConfig{public string pinHash{get;set;}="";}
public class V2AdminLogin{public string pin{get;set;}="";public string actor{get;set;}="";}
public class V2AdminPinChange{public string oldPin{get;set;}="";public string newPin{get;set;}="";public string actor{get;set;}="";}

public class V2BookingAction{public string status{get;set;}="محجوز";public string actor{get;set;}="";}
public class V2BookingMove{public string listId{get;set;}="";public string actor{get;set;}="";}

public class V2DeleteBooking{public string actor{get;set;}="";public string reason{get;set;}="";}

public class V2Settings{public int[] workingDays{get;set;}=[];public int duplicateNameDays{get;set;}=30;public bool requireMorning{get;set;}=true;public bool requireEvening{get;set;}=true;public DateTime alertsStartDate{get;set;}=DateTime.Today;}
public class V2SettingsUpdate{public string pin{get;set;}="";public string actor{get;set;}="";public V2Settings? settings{get;set;}}

public class V2ListEdit{public DateTime date{get;set;}public string doctor{get;set;}="";public string shift{get;set;}="صباحي";public string actor{get;set;}="";}
