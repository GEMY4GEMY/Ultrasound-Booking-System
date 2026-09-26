using System.Text.Json;

public static class ArabicV2Api
{
    public static void MapArabicV2(this WebApplication app, string dataDir)
    {
        var listsFile=Path.Combine(dataDir,"daily-lists.json");
        var bookingsFile=Path.Combine(dataDir,"bookings-v2.json");
        var auditFile=Path.Combine(dataDir,"audit-v2.json");
        var doctorsFile=Path.Combine(dataDir,"doctors-v2.json");
        Init(listsFile,"[]"); Init(bookingsFile,"[]"); Init(auditFile,"[]");
        Init(doctorsFile,JsonSerializer.Serialize(new[]{"د. أحمد","د. محمد"}));

        app.MapGet("/api/v2/doctors",()=>Results.Json(Read<string>(doctorsFile)));
        app.MapPost("/api/v2/doctors",async(HttpRequest r)=>{
            var x=await JsonSerializer.DeserializeAsync<V2DoctorInput>(r.Body);
            if(x==null||string.IsNullOrWhiteSpace(x.name)) return Results.BadRequest();
            var a=Read<string>(doctorsFile); if(!a.Contains(x.name.Trim()))a.Add(x.name.Trim()); Write(doctorsFile,a);
            Audit(auditFile,x.actor,"إضافة طبيب",x.name,r); return Results.Ok();
        });

        app.MapGet("/api/v2/lists",()=>Results.Json(Read<V2DailyList>(listsFile).OrderByDescending(x=>x.date)));
        app.MapPost("/api/v2/lists",async(HttpRequest r)=>{
            var x=await JsonSerializer.DeserializeAsync<V2DailyList>(r.Body);
            if(x==null||string.IsNullOrWhiteSpace(x.doctor)||string.IsNullOrWhiteSpace(x.shift))return Results.BadRequest();
            var a=Read<V2DailyList>(listsFile);
            if(a.Any(z=>z.date.Date==x.date.Date&&z.doctor==x.doctor&&z.shift==x.shift))return Results.Conflict(new{error="list_exists"});
            x.id=Guid.NewGuid().ToString("N")[..8].ToUpper(); x.state="مفتوحة"; x.createdAt=DateTime.Now; x.updatedAt=DateTime.Now;
            a.Add(x);Write(listsFile,a);Audit(auditFile,x.actor,"إنشاء قائمة",$"{x.date:yyyy-MM-dd} - {x.doctor} - {x.shift}",r);return Results.Ok(x);
        });
        app.MapPut("/api/v2/lists/{id}/state",async(string id,HttpRequest r)=>{
            var c=await JsonSerializer.DeserializeAsync<V2StateChange>(r.Body);var a=Read<V2DailyList>(listsFile);var x=a.FirstOrDefault(z=>z.id==id);
            if(c==null||x==null)return Results.NotFound();var old=x.state;x.state=c.state;x.updatedAt=DateTime.Now;x.modifiedBy=c.actor;Write(listsFile,a);
            Audit(auditFile,c.actor,"تغيير حالة القائمة",$"{x.doctor} - {x.shift}: {old} ← {x.state}",r);return Results.Ok(x);
        });

        app.MapGet("/api/v2/bookings",()=>Results.Json(Read<V2Booking>(bookingsFile).OrderByDescending(x=>x.createdAt)));
        app.MapPost("/api/v2/bookings",async(HttpRequest r)=>{
            var x=await JsonSerializer.DeserializeAsync<V2Booking>(r.Body);if(x==null)return Results.BadRequest();
            if(string.IsNullOrWhiteSpace(x.patientName)||string.IsNullOrWhiteSpace(x.exam)||!System.Text.RegularExpressions.Regex.IsMatch(x.phone??"","^\\d{11}$"))return Results.BadRequest(new{error="invalid_data"});
            var lists=Read<V2DailyList>(listsFile);var list=lists.FirstOrDefault(z=>z.id==x.listId);if(list==null)return Results.BadRequest(new{error="list_missing"});
            if(list.state!="مفتوحة")return Results.Conflict(new{error="list_locked"});
            var a=Read<V2Booking>(bookingsFile);x.id=Guid.NewGuid().ToString("N")[..8].ToUpper();
            if(string.IsNullOrWhiteSpace(x.patientId))x.patientId=NextPatientId(a);x.date=list.date;x.doctor=list.doctor;x.shift=list.shift;x.createdAt=x.updatedAt=DateTime.Now;
            a.Add(x);Write(bookingsFile,a);Audit(auditFile,x.actor,"إضافة حجز",$"{x.patientId} - {x.patientName} - {x.doctor} - {x.shift}",r);return Results.Ok(x);
        });
        app.MapPut("/api/v2/bookings/{id}",async(string id,HttpRequest r)=>{
            var n=await JsonSerializer.DeserializeAsync<V2Booking>(r.Body);var a=Read<V2Booking>(bookingsFile);var i=a.FindIndex(z=>z.id==id);
            if(n==null||i<0)return Results.NotFound();if(!System.Text.RegularExpressions.Regex.IsMatch(n.phone??"","^\\d{11}$"))return Results.BadRequest(new{error="invalid_phone"});
            var old=a[i];n.id=id;n.patientId=string.IsNullOrWhiteSpace(n.patientId)?old.patientId:n.patientId;n.createdAt=old.createdAt;n.updatedAt=DateTime.Now;a[i]=n;Write(bookingsFile,a);
            Audit(auditFile,n.actor,"تعديل حجز",$"{n.patientId} - {n.patientName}",r);return Results.Ok(n);
        });
        app.MapGet("/api/v2/audit",()=>Results.Json(Read<V2Audit>(auditFile).OrderByDescending(x=>x.time).Take(1000)));
        app.MapGet("/api/v2/admin/summary",()=>Results.Ok(new{
            doctors=Read<string>(doctorsFile).Count,
            lists=Read<V2DailyList>(listsFile).Count,
            bookings=Read<V2Booking>(bookingsFile).Count,
            dataPath=dataDir,
            server=Environment.MachineName
        }));
        app.MapGet("/api/v2/alerts",()=>{
            var lists=Read<V2DailyList>(listsFile);var today=DateTime.Today;var alerts=new List<V2Alert>();
            var dates=lists.Select(x=>x.date.Date).Distinct().Where(d=>d<=today).OrderByDescending(d=>d).ToList();
            foreach(var d in dates){
                var day=lists.Where(x=>x.date.Date==d).ToList();
                var morning=day.Any(x=>x.shift=="صباحي"),evening=day.Any(x=>x.shift=="مسائي");
                if(d<today&&day.Any(x=>x.state!="مكتملة"))
                    alerts.Add(new V2Alert{type="old_incomplete",date=d,severity="danger",message=$"قوائم سابقة غير مكتملة بتاريخ {d:dd/MM/yyyy}"});
                if(d==today&&day.Count>0&&(!morning||!evening))
                    alerts.Add(new V2Alert{type="today_missing_shift",date=d,severity="warning",message=!morning?"لم يتم إنشاء القائمة الصباحية لليوم":"لم يتم إنشاء القائمة المسائية لليوم"});
                if(d<today&&day.Count>0&&(!morning||!evening))
                    alerts.Add(new V2Alert{type="old_missing_shift",date=d,severity="warning",message=$"كان هناك شفت غير منشأ بتاريخ {d:dd/MM/yyyy}"});
            }
            if(!lists.Any(x=>x.date.Date==today))
                alerts.Add(new V2Alert{type="today_no_lists",date=today,severity="danger",message="لم يتم إنشاء أي قائمة لليوم حتى الآن"});
            return Results.Json(alerts.OrderByDescending(x=>x.date).ThenBy(x=>x.type));
        });
    }
    static string NextPatientId(List<V2Booking> a){var n=a.Select(x=>int.TryParse((x.patientId??"").Replace("P",""),out var v)?v:0).DefaultIfEmpty(0).Max()+1;return $"P{n:000000}";}
    static void Audit(string f,string actor,string action,string detail,HttpRequest r){var a=Read<V2Audit>(f);a.Add(new V2Audit{time=DateTime.Now,actor=string.IsNullOrWhiteSpace(actor)?"غير محدد":actor,action=action,detail=detail,device=r.Headers["User-Agent"].ToString()});Write(f,a);}
    static void Init(string p,string v){if(!File.Exists(p))File.WriteAllText(p,v);}
    static List<T> Read<T>(string p)=>JsonSerializer.Deserialize<List<T>>(File.ReadAllText(p))??[];
    static void Write<T>(string p,IEnumerable<T> x)=>File.WriteAllText(p,JsonSerializer.Serialize(x,new JsonSerializerOptions{WriteIndented=true}));
}
public class V2DailyList{public string id{get;set;}="";public DateTime date{get;set;}public string doctor{get;set;}="";public string shift{get;set;}="صباحي";public string state{get;set;}="مفتوحة";public string actor{get;set;}="";public string modifiedBy{get;set;}="";public DateTime createdAt{get;set;}public DateTime updatedAt{get;set;}}
public class V2Booking{public string id{get;set;}="";public string listId{get;set;}="";public string patientId{get;set;}="";public string patientName{get;set;}="";public string phone{get;set;}="";public string contractType{get;set;}="نقدي";public string exam{get;set;}="";public string doctor{get;set;}="";public string shift{get;set;}="";public DateTime date{get;set;}public string notes{get;set;}="";public string actor{get;set;}="";public DateTime createdAt{get;set;}public DateTime updatedAt{get;set;}}
public class V2Audit{public DateTime time{get;set;}public string actor{get;set;}="";public string action{get;set;}="";public string detail{get;set;}="";public string device{get;set;}="";}
public class V2StateChange{public string state{get;set;}="";public string actor{get;set;}="";}
public class V2DoctorInput{public string name{get;set;}="";public string actor{get;set;}="";}

public class V2Alert{public string type{get;set;}="";public DateTime date{get;set;}public string severity{get;set;}="warning";public string message{get;set;}="";}
