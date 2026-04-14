using KontakteDB.Data;
using KontakteDB.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KontakteDB.Controllers;

[Authorize]
public class SearchController : Controller
{
    private readonly AppDbContext _db;

    public SearchController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? q)
    {
        var vm = new SearchViewModel { Query = q };

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            vm.Searched = true;

            vm.Companies = await _db.Companies
                .Where(c =>
                    c.Name.Contains(term) ||
                    (c.City != null && c.City.Contains(term)) ||
                    (c.Email != null && c.Email.Contains(term)) ||
                    (c.Phone != null && c.Phone.Contains(term)) ||
                    (c.Notes != null && c.Notes.Contains(term)))
                .OrderBy(c => c.Name)
                .Take(30)
                .ToListAsync();

            vm.Contacts = await _db.Contacts
                .Include(c => c.Company)
                .Where(c =>
                    c.LastName.Contains(term) ||
                    (c.FirstName != null && c.FirstName.Contains(term)) ||
                    (c.Email != null && c.Email.Contains(term)) ||
                    (c.Phone != null && c.Phone.Contains(term)) ||
                    (c.Mobile != null && c.Mobile.Contains(term)) ||
                    (c.Position != null && c.Position.Contains(term)) ||
                    (c.City != null && c.City.Contains(term)) ||
                    (c.Notes != null && c.Notes.Contains(term)) ||
                    (c.Company != null && c.Company.Name.Contains(term)))
                .OrderBy(c => c.LastName)
                .Take(50)
                .ToListAsync();
        }

        return View(vm);
    }
}
