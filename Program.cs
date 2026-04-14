using KontakteDB.Controllers;
using KontakteDB.Data;
using KontakteDB.Models;
using KontakteDB.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Datenbank ─────────────────────────────────────────────────────────────────
// Standardmäßig SQLite; für SQL Server die Connection String in appsettings.json
// unter "SqlServer" hinterlegen und UseDatabase auf "SqlServer" setzen.
var dbProvider = builder.Configuration.GetValue<string>("UseDatabase") ?? "SQLite";

if (dbProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDbContext<AppDbContext>(opt =>
        opt.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));
}
else
{
    // Absoluten Pfad verwenden, damit SQLite in jeder Hosting-Umgebung schreiben kann
    var dbPath = $"Data Source={Path.Combine(builder.Environment.ContentRootPath, "kontakte.db")}";
    builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(dbPath));
}

// ── Authentifizierung ─────────────────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath  = "/Account/Login";
        opt.LogoutPath = "/Account/Logout";
        opt.AccessDeniedPath = "/Account/Login";
        opt.ExpireTimeSpan   = TimeSpan.FromDays(7);
        opt.SlidingExpiration = true;
        opt.Cookie.HttpOnly  = true;
        opt.Cookie.SameSite  = SameSiteMode.Lax;
    });

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<ImportService>();

// ── Railway / Cloud: PORT-Umgebungsvariable respektieren ─────────────────────
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

// ── Forwarded Headers (Railway / jeder Reverse Proxy) ────────────────────────
// Damit erkennt ASP.NET Core das echte Schema (https) und die echte Client-IP.
// Ohne dies schlägt die Antiforgery-Validierung fehl → HTTP 500 beim Login-POST.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// ── Middleware ────────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

// HTTPS-Redirect deaktiviert: Railway terminiert TLS am eigenen Proxy
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ── DB-Initialisierung: Migration anwenden + Admin-User anlegen ───────────────
using (var scope = app.Services.CreateScope())
{
    var db  = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        db.Database.Migrate();

        if (!db.Users.Any())
        {
            db.Users.Add(new AppUser
            {
                Username     = "admin",
                PasswordHash = AccountController.HashPassword("Admin1234!"),
                DisplayName  = "Administrator",
                Role         = "Admin",
                IsActive     = true,
                CreatedAt    = DateTime.UtcNow
            });
            db.SaveChanges();
            log.LogInformation("Admin-Benutzer angelegt.");
        }
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Fehler bei der DB-Initialisierung.");
    }
}

app.Run();
