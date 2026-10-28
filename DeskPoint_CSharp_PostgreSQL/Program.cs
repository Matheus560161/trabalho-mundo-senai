using System.Security.Cryptography;
using System.Text;
using EquipeEmPonto.Data;
using EquipeEmPonto.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options => {
    options.LoginPath = "/Admin/Login"; options.AccessDeniedPath = "/Admin/Login";
    options.Cookie.HttpOnly = true; options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8); options.SlidingExpiration = true;
});
builder.Services.AddAuthorization();
var app = builder.Build();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }
app.UseStaticFiles(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
using (var scope = app.Services.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var email = config["Admin:Email"]; var password = config["Admin:Password"];
    if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password) && !await db.AdminUsers.AnyAsync()) {
        db.AdminUsers.Add(new AdminUser { Email = email.Trim().ToLowerInvariant(), PasswordHash = HashPassword(password) }); await db.SaveChangesAsync();
    }
}
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapPost("/api/ponto/rfid", async (HttpRequest request, AppDbContext db, IConfiguration config) => {
    var expectedKey = config["Rfid:ApiKey"]; var providedKey = request.Headers["X-Device-Key"].ToString();
    if (string.IsNullOrWhiteSpace(expectedKey) || !SafeEquals(expectedKey, providedKey)) return Results.Unauthorized();
    var body = await request.ReadFromJsonAsync<RfidPunchRequest>();
    if (body is null || string.IsNullOrWhiteSpace(body.CardUid)) return Results.BadRequest(new { message = "UID do cartão não informado." });
    var uid = body.CardUid.Trim().Replace(" ", "").ToUpperInvariant();
    var employee = await db.Employees.FirstOrDefaultAsync(e => e.RfidUid == uid && e.Active);
    if (employee is null) return Results.NotFound(new { message = "Cartão não cadastrado para um funcionário ativo." });
    var now = DateTimeOffset.UtcNow; var start = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
    var last = await db.Punches.Where(p => p.EmployeeId == employee.Id && p.PunchedAt >= start).OrderByDescending(p => p.PunchedAt).FirstOrDefaultAsync();
    var kind = last?.Kind == "entrada" ? "saída" : "entrada";
    var punch = new Punch { EmployeeId = employee.Id, PunchedAt = now, Kind = kind }; db.Punches.Add(punch); await db.SaveChangesAsync();
    return Results.Ok(new { employee = employee.Name, kind, at = punch.PunchedAt });
});
app.Run();
static string HashPassword(string password) { var salt = RandomNumberGenerator.GetBytes(16); var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32); return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}"; }
static bool SafeEquals(string a, string b) { var aa = Encoding.UTF8.GetBytes(a); var bb = Encoding.UTF8.GetBytes(b); return aa.Length == bb.Length && CryptographicOperations.FixedTimeEquals(aa, bb); }
public record RfidPunchRequest(string CardUid);
