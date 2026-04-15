using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using KontakteDB.Data;
using KontakteDB.ViewModels;
using Markdig;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KontakteDB.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _db;

    public AccountController(AppDbContext db) => _db = db;

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var hash = HashPassword(model.Passwort);
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == model.Benutzername
                                   && u.PasswordHash == hash
                                   && u.IsActive);

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Benutzername oder Passwort ist falsch.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.GivenName, user.DisplayName ?? user.Username),
            new(ClaimTypes.Role, user.Role)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true });

        var returnUrl = model.ReturnUrl;
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult Dokumentation()
    {
        var readmePath = Path.Combine(Directory.GetCurrentDirectory(), "README.md");
        if (!System.IO.File.Exists(readmePath))
            return NotFound("README.md nicht gefunden.");

        var markdownText = System.IO.File.ReadAllText(readmePath);
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var htmlBody = Markdown.ToHtml(markdownText, pipeline);

        var fullHtml = $$"""
            <!DOCTYPE html>
            <html lang="de">
            <head>
                <meta charset="utf-8" />
                <title>Anwenderdokumentation – KontakteDB</title>
                <style>
                    body {
                        font-family: 'Segoe UI', Arial, sans-serif;
                        max-width: 860px;
                        margin: 40px auto;
                        padding: 0 24px 60px;
                        color: #1a1a2e;
                        line-height: 1.7;
                    }
                    h1, h2, h3, h4 { color: #1a1a2e; margin-top: 1.5em; }
                    h1 { border-bottom: 2px solid #c8a96e; padding-bottom: 6px; }
                    h2 { border-bottom: 1px solid #e0e0e0; padding-bottom: 4px; }
                    code { background: #f4f4f4; padding: 2px 6px; border-radius: 3px; font-size: 0.88em; }
                    pre { background: #f4f4f4; padding: 14px; border-radius: 6px; overflow-x: auto; }
                    pre code { background: none; padding: 0; }
                    table { border-collapse: collapse; width: 100%; margin: 1em 0; }
                    th, td { border: 1px solid #d0d0d0; padding: 8px 12px; text-align: left; }
                    th { background: #f0f0f0; font-weight: 600; }
                    blockquote { border-left: 4px solid #c8a96e; margin: 0; padding: 8px 16px; background: #fdf9f2; }
                    a { color: #c8a96e; }
                    img { max-width: 100%; }
                    @media print {
                        body { margin: 0; padding: 20px; }
                        .no-print { display: none; }
                    }
                </style>
            </head>
            <body>
                <div class="no-print" style="background:#1a1a2e;color:#fff;padding:10px 20px;margin:-40px -24px 30px;display:flex;align-items:center;gap:12px;">
                    <span style="font-weight:600;">KontakteDB – Anwenderdokumentation</span>
                    <button onclick="window.print()" style="margin-left:auto;background:#c8a96e;color:#fff;border:none;padding:6px 16px;border-radius:4px;cursor:pointer;font-size:0.9rem;">Als PDF speichern</button>
                </div>
                {{htmlBody}}
                <script>window.print();</script>
            </body>
            </html>
            """;

        return Content(fullHtml, "text/html", Encoding.UTF8);
    }

    public static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
