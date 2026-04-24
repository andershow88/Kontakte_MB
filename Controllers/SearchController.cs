using KontakteDB.Data;
using KontakteDB.Services;
using KontakteDB.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KontakteDB.Controllers;

[Authorize]
public class SearchController : Controller
{
    private readonly AppDbContext _db;
    private readonly SmartSearchService _smartSearch;

    public SearchController(AppDbContext db, SmartSearchService smartSearch)
    {
        _db = db;
        _smartSearch = smartSearch;
    }

    public async Task<IActionResult> Index(string? q, string mode = "smart")
    {
        var vm = new SearchViewModel { Query = q, Mode = mode };

        if (!string.IsNullOrWhiteSpace(q))
        {
            vm.Searched = true;

            if (mode == "ai")
            {
                // AI mode: just pass query through, JS handles the AI call
            }
            else
            {
                var result = await _smartSearch.SearchAsync(q);
                vm.ScoredCompanies = result.Companies;
                vm.ScoredContacts = result.Contacts;
                vm.ElapsedMs = result.ElapsedMs;
                vm.MaxCompanyScore = result.Companies.Any()
                    ? result.Companies.Max(c => c.Score) : 1;
                vm.MaxContactScore = result.Contacts.Any()
                    ? result.Contacts.Max(c => c.Score) : 1;
            }
        }

        return View(vm);
    }
}
