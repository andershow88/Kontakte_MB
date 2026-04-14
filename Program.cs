using KontakteDB.Controllers;
using KontakteDB.Data;
using KontakteDB.Models;
using KontakteDB.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
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
    builder.Services.AddDbContext<AppDbContext>(opt =>
        opt.UseSqlite(builder.Configuration.GetConnectionString("SQLite")
            ?? "Data Source=kontakte.db"));
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

// ── Middleware ────────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // HSTS und HTTPS-Redirect deaktiviert: Railway terminiert TLS am Proxy
}

// app.UseHttpsRedirection(); // nicht auf Railway (TLS-Terminierung am Reverse Proxy)
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
