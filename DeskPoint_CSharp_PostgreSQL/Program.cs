using System.Security.Cryptography;
using System.Text;
using EquipeEmPonto.Data;
using EquipeEmPonto.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(int.Parse(port));
});

var dbConnectionString = NormalizePostgresConnectionString(
    builder.Configuration.GetConnectionString("Postgres")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING"));

if (string.IsNullOrWhiteSpace(dbConnectionString))
{
    throw new InvalidOperationException("No PostgreSQL connection string configured. Set ConnectionStrings__Postgres, DATABASE_URL, or POSTGRES_CONNECTION_STRING.");
}

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(dbConnectionString));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/Admin/Login";
    options.AccessDeniedPath = "/Admin/Login";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();

    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var email = config["Admin:Email"];
    var password = config["Admin:Password"];

    if (!string.IsNullOrWhiteSpace(email) &&
        !string.IsNullOrWhiteSpace(password) &&
        !await db.AdminUsers.AnyAsync())
    {
        db.AdminUsers.Add(new AdminUser
        {
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = HashPassword(password)
        });

        await db.SaveChangesAsync();
    }
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/ponto/rfid", async (HttpRequest request, AppDbContext db, IConfiguration config) =>
{
    var expectedKey = config["Rfid:ApiKey"];
    var providedKey = request.Headers["X-Device-Key"].ToString();

    if (string.IsNullOrWhiteSpace(expectedKey) || !SafeEquals(expectedKey, providedKey))
        return Results.Unauthorized();

    var body = await request.ReadFromJsonAsync<RfidPunchRequest>();

    if (body is null || string.IsNullOrWhiteSpace(body.CardUid))
        return Results.BadRequest(new { message = "UID do cartão não informado." });

    var uid = body.CardUid.Trim().Replace(" ", "").ToUpperInvariant();

    var employee = await db.Employees.FirstOrDefaultAsync(e => e.RfidUid == uid && e.Active);

    if (employee is null)
        return Results.NotFound(new { message = "Cartão não cadastrado para um funcionário ativo." });

    // Horário de Brasília
    var fusoBrasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    var nowBrasilia = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, fusoBrasilia);

    // Busca o último registro, independentemente do dia
    var last = await db.Punches
        .Where(p => p.EmployeeId == employee.Id)
        .OrderByDescending(p => p.PunchedAt)
        .FirstOrDefaultAsync();

    // Alterna entre entrada e saída
    var kind = last?.Kind == "entrada" ? "saída" : "entrada";

    // Salva no banco em UTC
    var punch = new Punch
    {
        EmployeeId = employee.Id,
        PunchedAt = DateTimeOffset.UtcNow,
        Kind = kind
    };

    db.Punches.Add(punch);
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        employee = employee.Name,
        kind,
        at = nowBrasilia
    });
});

app.Run();

static string NormalizePostgresConnectionString(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return string.Empty;
    }

    var value = connectionString.Trim();

    if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        && !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        return value;
    }

    var uri = new Uri(value);

    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = uri.AbsolutePath.Trim('/'),
        Username = string.Empty,
        Password = string.Empty,
        SslMode = SslMode.Require
    };

    if (!string.IsNullOrWhiteSpace(uri.UserInfo))
    {
        var parts = uri.UserInfo.Split(':', 2);
        builder.Username = parts[0];

        if (parts.Length > 1)
            builder.Password = parts[1];
    }

    if (!string.IsNullOrWhiteSpace(uri.Query))
    {
        var query = uri.Query.TrimStart('?');

        foreach (var parameter in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = parameter.Split('=', 2);

            if (pair.Length != 2)
                continue;

            var key = Uri.UnescapeDataString(pair[0]);
            var val = Uri.UnescapeDataString(pair[1]);

            if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse<SslMode>(val, true, out var sslMode))
                {
                    builder.SslMode = sslMode;
                }
            }
        }
    }

    return builder.ConnectionString;
}

static string HashPassword(string password)
{
    var salt = RandomNumberGenerator.GetBytes(16);

    var hash = Rfc2898DeriveBytes.Pbkdf2(
        password,
        salt,
        100_000,
        HashAlgorithmName.SHA256,
        32);

    return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
}

static bool SafeEquals(string a, string b)
{
    var aa = Encoding.UTF8.GetBytes(a);
    var bb = Encoding.UTF8.GetBytes(b);

    return aa.Length == bb.Length &&
           CryptographicOperations.FixedTimeEquals(aa, bb);
}

public record RfidPunchRequest(string CardUid);