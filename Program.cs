using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5090");
var app = builder.Build();
var root = AppContext.BaseDirectory;
var dataDir = Path.Combine(root,"data"); Directory.CreateDirectory(dataDir);
var db = Path.Combine(dataDir,"bookings.json");
var users = Path.Combine(dataDir,"users.json");
if(!File.Exists(db)) File.WriteAllText(db,"[]");
if(!File.Exists(users)) File.WriteAllText(users, JsonSerializer.Serialize(new[]{new {username="admin",password=Hash("admin123"),role="Admin",name="Administrator"}}));
app.UseDefaultFiles(); app.UseStaticFiles();

app.MapPost("/api/login", async (HttpRequest req)=>{
 var x=await JsonSerializer.DeserializeAsync<Login>(req.Body); if(x is null) return Results.BadRequest();
 var us=JsonSerializer.Deserialize<List<User>>(File.ReadAllText(users))??[];
 var u=us.FirstOrDefault(a=>a.username.Equals(x.username,StringComparison.OrdinalIgnoreCase)&&a.password==Hash(x.password));
 return u is null?Results.Unauthorized():Results.Ok(new{u.username,u.role,u.name});
});
app.MapGet("/api/bookings",()=>Results.Text(File.ReadAllText(db),"application/json"));
app.MapPost("/api/bookings", async (HttpRequest req)=>{
 var b=await JsonSerializer.DeserializeAsync<Booking>(req.Body); if(b is null)return Results.BadRequest();
 var list=JsonSerializer.Deserialize<List<Booking>>(File.ReadAllText(db))??[];
 b.id=Guid.NewGuid().ToString("N")[..8].ToUpper(); b.createdAt=DateTime.Now; b.updatedAt=DateTime.Now;
 list.Add(b); Save(list); return Results.Ok(b);
});
app.MapPut("/api/bookings/{id}", async (string id,HttpRequest req)=>{
 var b=await JsonSerializer.DeserializeAsync<Booking>(req.Body); var list=JsonSerializer.Deserialize<List<Booking>>(File.ReadAllText(db))??[];
 var i=list.FindIndex(x=>x.id==id); if(i<0||b is null)return Results.NotFound();
 b.id=id;b.createdAt=list[i].createdAt;b.updatedAt=DateTime.Now;list[i]=b;Save(list);return Results.Ok(b);
});
app.MapDelete("/api/bookings/{id}",(string id)=>{
 var list=JsonSerializer.Deserialize<List<Booking>>(File.ReadAllText(db))??[]; var n=list.RemoveAll(x=>x.id==id);Save(list);return n>0?Results.Ok():Results.NotFound();
});
app.MapGet("/api/backup",()=>{var p=Path.Combine(dataDir,$"backup_{DateTime.Now:yyyyMMdd_HHmmss}.json");File.Copy(db,p);return Results.Ok(new{file=Path.GetFileName(p)});});
app.Run();
void Save(List<Booking> x)=>File.WriteAllText(db,JsonSerializer.Serialize(x,new JsonSerializerOptions{WriteIndented=true}));
static string Hash(string s)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
record Login(string username,string password);
record User(string username,string password,string role,string name);
class Booking {public string id{get;set;}="";public string patientName{get;set;}="";public string exam{get;set;}="";public string doctor{get;set;}="";public DateTime bookingDate{get;set;}public string shift{get;set;}="Morning";public bool papersReceived{get;set;}public string notes{get;set;}="";public string bookedBy{get;set;}="";public string modifiedBy{get;set;}="";public DateTime createdAt{get;set;}public DateTime updatedAt{get;set;}}