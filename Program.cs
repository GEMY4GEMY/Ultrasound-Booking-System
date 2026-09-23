using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
var builder=WebApplication.CreateBuilder(args);builder.WebHost.UseUrls("http://0.0.0.0:5090");var app=builder.Build();
var dir=Path.Combine(AppContext.BaseDirectory,"data");Directory.CreateDirectory(dir);
string B=Path.Combine(dir,"bookings.json"),U=Path.Combine(dir,"users.json"),D=Path.Combine(dir,"doctors.json"),E=Path.Combine(dir,"exams.json"),A=Path.Combine(dir,"activity.json");
Init(B,"[]");Init(D,JsonSerializer.Serialize(new[]{"Dr. Ahmed","Dr. Mohamed"}));Init(E,JsonSerializer.Serialize(new[]{"Abdominal Ultrasound","Pelvic Ultrasound","Doppler","Echocardiography"}));Init(A,"[]");
if(!File.Exists(U))
{
    var initialUsers = new List<User>
    {
        new User
        {
            username = "admin",
            password = Hash("admin123"),
            name = "Administrator",
            role = "Super Admin",
            permissions = new[] { "bookings.view", "bookings.add", "bookings.edit", "bookings.delete", "users.manage", "masters.manage", "backup", "reports" }
        }
    };
    File.WriteAllText(U, JsonSerializer.Serialize(initialUsers));
}
app.UseDefaultFiles();app.UseStaticFiles();
app.MapPost("/api/login",async(HttpRequest r)=>{var x=await JsonSerializer.DeserializeAsync<Login>(r.Body);var us=Read<User>(U);var u=us.FirstOrDefault(z=>x!=null&&z.username.Equals(x.username,StringComparison.OrdinalIgnoreCase)&&z.password==Hash(x.password));if(u==null)return Results.Unauthorized(); return Results.Ok(new { username=u.username, name=u.name, role=u.role, permissions=u.permissions });});
app.MapGet("/api/bookings",()=>Json(Read<Booking>(B)));app.MapPost("/api/bookings",async(HttpRequest r)=>{var x=await JsonSerializer.DeserializeAsync<Booking>(r.Body);if(x==null)return Results.BadRequest();var a=Read<Booking>(B);x.id=Guid.NewGuid().ToString("N")[..8].ToUpper();x.createdAt=x.updatedAt=DateTime.Now;a.Add(x);Write(B,a);Log(x.bookedBy,"ADD BOOKING",x.id+" - "+x.patientName);return Results.Ok(x);});
app.MapPut("/api/bookings/{id}",async(string id,HttpRequest r)=>{var x=await JsonSerializer.DeserializeAsync<Booking>(r.Body);var a=Read<Booking>(B);var i=a.FindIndex(z=>z.id==id);if(x==null||i<0)return Results.NotFound();x.id=id;x.createdAt=a[i].createdAt;x.updatedAt=DateTime.Now;a[i]=x;Write(B,a);Log(x.modifiedBy,"EDIT BOOKING",id+" - "+x.patientName);return Results.Ok(x);});
app.MapDelete("/api/bookings/{id}",(string id,string user)=>{var a=Read<Booking>(B);var x=a.FirstOrDefault(z=>z.id==id);if(x==null)return Results.NotFound();a.Remove(x);Write(B,a);Log(user,"DELETE BOOKING",id+" - "+x.patientName);return Results.Ok();});
app.MapGet("/api/users",()=>Json(Read<User>(U).Select(x=>new{x.username,x.name,x.role,x.permissions})));
app.MapPost("/api/users",async(HttpRequest r)=>{var x=await JsonSerializer.DeserializeAsync<UserInput>(r.Body);if(x==null||string.IsNullOrWhiteSpace(x.username)||string.IsNullOrWhiteSpace(x.password))return Results.BadRequest();var a=Read<User>(U);if(a.Any(z=>z.username.Equals(x.username,StringComparison.OrdinalIgnoreCase)))return Results.Conflict();a.Add(new User{username=x.username,name=x.name,password=Hash(x.password),role=x.role,permissions=x.permissions??[]});Write(U,a);Log(x.actor,"ADD USER",x.username);return Results.Ok();});
app.MapPut("/api/users/{name}",async(string name,HttpRequest r)=>{var x=await JsonSerializer.DeserializeAsync<UserUpdate>(r.Body);if(x==null)return Results.BadRequest();var a=Read<User>(U);var u=a.FirstOrDefault(z=>z.username==name);if(u==null)return Results.NotFound();u.name=x.name;u.role=x.role;u.permissions=x.permissions??[];if(!string.IsNullOrWhiteSpace(x.password))u.password=Hash(x.password);Write(U,a);Log(x.actor,"UPDATE USER",name);return Results.Ok();});
app.MapPost("/api/change-password",async(HttpRequest r)=>{var x=await JsonSerializer.DeserializeAsync<PasswordChange>(r.Body);if(x==null)return Results.BadRequest();var a=Read<User>(U);var u=a.FirstOrDefault(z=>z.username==x.username&&z.password==Hash(x.oldPassword));if(u==null)return Results.Unauthorized();u.password=Hash(x.newPassword);Write(U,a);Log(u.name,"CHANGE PASSWORD",u.username);return Results.Ok();});
app.MapDelete("/api/users/{name}",(string name,string actor)=>{if(name=="admin")return Results.BadRequest();var a=Read<User>(U);a.RemoveAll(x=>x.username==name);Write(U,a);Log(actor,"DELETE USER",name);return Results.Ok();});
app.MapGet("/api/doctors",()=>Json(Read<string>(D)));app.MapPost("/api/doctors",async(HttpRequest r)=>{var x=await JsonSerializer.DeserializeAsync<Master>(r.Body);if(x==null)return Results.BadRequest();var a=Read<string>(D);if(!a.Contains(x.value,StringComparer.OrdinalIgnoreCase))a.Add(x.value);Write(D,a);Log(x.actor,"ADD DOCTOR",x.value);return Results.Ok();});
app.MapGet("/api/exams",()=>Json(Read<string>(E)));app.MapPost("/api/exams",async(HttpRequest r)=>{var x=await JsonSerializer.DeserializeAsync<Master>(r.Body);if(x==null)return Results.BadRequest();var a=Read<string>(E);if(!a.Contains(x.value,StringComparer.OrdinalIgnoreCase))a.Add(x.value);Write(E,a);Log(x.actor,"ADD EXAM",x.value);return Results.Ok();});
app.MapGet("/api/activity",()=>Json(Read<Activity>(A).OrderByDescending(x=>x.time).Take(500)));
app.MapGet("/api/backup",()=>{var stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");var bd=Path.Combine(dir,"backups",stamp);Directory.CreateDirectory(bd);foreach(var f in new[]{B,U,D,E,A})File.Copy(f,Path.Combine(bd,Path.GetFileName(f)),true);return Results.Ok(new{folder=stamp});});app.Run();
void Init(string p,string v){if(!File.Exists(p))File.WriteAllText(p,v);}List<T> Read<T>(string p)=>JsonSerializer.Deserialize<List<T>>(File.ReadAllText(p))??[];void Write<T>(string p,IEnumerable<T> x)=>File.WriteAllText(p,JsonSerializer.Serialize(x,new JsonSerializerOptions{WriteIndented=true}));IResult Json(object x)=>Results.Json(x);
void Log(string user,string action,string detail){var a=Read<Activity>(A);a.Add(new Activity{time=DateTime.Now,user=user,action=action,detail=detail});Write(A,a);}
static string Hash(string s)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
record Login(string username,string password);record Master(string value,string actor);
class User{public string username{get;set;}="";public string password{get;set;}="";public string name{get;set;}="";public string role{get;set;}="User";public string[] permissions{get;set;}=[];}class UserInput:User{public string actor{get;set;}="";} class UserUpdate{public string name{get;set;}="";public string role{get;set;}="User";public string password{get;set;}="";public string[] permissions{get;set;}=[];public string actor{get;set;}="";} record PasswordChange(string username,string oldPassword,string newPassword);
class Activity{public DateTime time{get;set;}public string user{get;set;}="";public string action{get;set;}="";public string detail{get;set;}="";}
class Booking{public string id{get;set;}="";public string patientName{get;set;}="";public string exam{get;set;}="";public string doctor{get;set;}="";public DateTime bookingDate{get;set;}public string shift{get;set;}="Morning";public bool papersReceived{get;set;}public string notes{get;set;}="";public string bookedBy{get;set;}="";public string modifiedBy{get;set;}="";public DateTime createdAt{get;set;}public DateTime updatedAt{get;set;}}