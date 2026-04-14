using KontakteDB.Data;
using KontakteDB.Services;
using KontakteDB.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KontakteDB.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly AppDbContext _db;
    private readonly ImportService _importService;

    public HomeController(AppDbContext db, ImportService importService)
    {
        _db = db;
        _importService = importService;
    }

    public async Task<IActionResult> Index()
    {
        var vm = new DashboardViewModel
        {
            TotalCompanies    = await _db.Companies.CountAsync(),
            TotalContacts     = await _db.Contacts.CountAsync(),
            FavoriteCompanies = await _db.Companies.CountAsync(c => c.IsFavorite),
            FavoriteContacts  = await _db.Contacts.CountAsync(c => c.IsFavorite),
            ContactsWithEmail = await _db.Contacts.CountAsync(c => c.Email != null && c.Email != ""),
            ContactsWithPhone = await _db.Contacts.CountAsync(c => c.Phone != null && c.Phone != ""),

            RecentCompanies = await _db.Companies
                .OrderByDescending(c => c.UpdatedAt)
                .Take(5)
                .ToListAsync(),

            RecentContacts = await _db.Contacts
                .Include(c => c.Company)
                .OrderByDescending(c => c.UpdatedAt)
                .Take(8)
                .ToListAsync(),

            FavoriteCompaniesList = await _db.Companies
                .Where(c => c.IsFavorite)
                .OrderBy(c => c.Name)
                .Take(6)
                .ToListAsync()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import()
    {
        if (await _db.Companies.AnyAsync())
        {
            TempData["Warning"] = "Import abgebrochen: Die Datenbank enthält bereits Firmen. Bitte zuerst die Datenbank zurücksetzen.";
            return RedirectToAction("Index");
        }

        var result = await _importService.RunImportAsync();
        if (result.Success)
            TempData["Success"] = $"Import erfolgreich: {result.CompaniesImported} Firmen und {result.ContactsImported} Kontakte importiert.";
        else
            TempData["Error"] = $"Import-Fehler: {result.Error}";

        return RedirectToAction("Index");
    }
}
