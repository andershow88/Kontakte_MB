using KontakteDB.Data;
using KontakteDB.Models;
using KontakteDB.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KontakteDB.Controllers;

[Authorize]
public class CompaniesController : Controller
{
    private readonly AppDbContext _db;

    public CompaniesController(AppDbContext db) => _db = db;

    // ── Liste ─────────────────────────────────────────────────────────────────

    public async Task<IActionResult> Index(
        string? q, string? city, string? country,
        string? sortBy, string? sortDir,
        int page = 1, int pageSize = 50)
    {
        var query = _db.Companies.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c =>
                c.Name.Contains(term) ||
                (c.City != null && c.City.Contains(term)) ||
                (c.Email != null && c.Email.Contains(term)) ||
                (c.Phone != null && c.Phone.Contains(term)) ||
                (c.Notes != null && c.Notes.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(c => c.City == city);

        if (!string.IsNullOrWhiteSpace(country))
            query = query.Where(c => c.Country == country);

        // Sortierung
        query = (sortBy?.ToLower(), sortDir?.ToLower()) switch
        {
            ("name", "desc")    => query.OrderByDescending(c => c.Name),
            ("city", "asc")     => query.OrderBy(c => c.City),
            ("city", "desc")    => query.OrderByDescending(c => c.City),
            ("updated", "asc")  => query.OrderBy(c => c.UpdatedAt),
            ("updated", "desc") => query.OrderByDescending(c => c.UpdatedAt),
            ("created", "asc")  => query.OrderBy(c => c.CreatedAt),
            ("created", "desc") => query.OrderByDescending(c => c.CreatedAt),
            _                   => query.OrderBy(c => c.Name)
        };

        var total = await query.CountAsync();
        var companies = await query
            .Include(c => c.Contacts.Where(ct => !ct.IsDeleted))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var vm = new CompanyListViewModel
        {
            Companies  = companies,
            SearchTerm = q,
            FilterCity = city,
            FilterCountry = country,
            SortBy     = sortBy,
            SortDir    = sortDir,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize,
            Cities     = await _db.Companies.Where(c => c.City != null && c.City != "")
                             .Select(c => c.City!).Distinct().OrderBy(x => x).ToListAsync(),
            Countries  = await _db.Companies.Where(c => c.Country != null && c.Country != "")
                             .Select(c => c.Country!).Distinct().OrderBy(x => x).ToListAsync()
        };

        return View(vm);
    }

    // ── Details ───────────────────────────────────────────────────────────────

    public async Task<IActionResult> Details(int id)
    {
        var company = await _db.Companies
            .Include(c => c.Contacts.Where(ct => !ct.IsDeleted))
            .FirstOrDefaultAsync(c => c.Id == id);

        if (company is null) return NotFound();
        return View(company);
    }

    // ── Anlegen ───────────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Create() => View(new Company());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Company model)
    {
        if (!ModelState.IsValid) return View(model);

        model.CreatedAt = DateTime.UtcNow;
        model.UpdatedAt = DateTime.UtcNow;
        _db.Companies.Add(model);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Firma {model.Name} wurde angelegt.";
        return RedirectToAction("Details", new { id = model.Id });
    }

    [HttpGet("api/companies/check-duplicate")]
    public async Task<IActionResult> CheckDuplicateCompany(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length < 2)
            return Ok(new { duplicates = Array.Empty<object>() });

        var term = name.Trim().ToLower();
        var matches = await _db.Companies
            .Where(c => c.Name.ToLower().Contains(term) || term.Contains(c.Name.ToLower()))
            .OrderBy(c => c.Name)
            .Take(5)
            .Select(c => new {
                c.Id, c.Name, c.City, c.Phone, c.Email,
                contacts = c.Contacts.Count(ct => !ct.IsDeleted)
            })
            .ToListAsync();

        return Ok(new { duplicates = matches });
    }

    [HttpPost("api/companies/create")]
    public async Task<IActionResult> CreateApi([FromBody] Company model)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = "Firmenname ist erforderlich." });

        model.CreatedAt = DateTime.UtcNow;
        model.UpdatedAt = DateTime.UtcNow;
        _db.Companies.Add(model);
        await _db.SaveChangesAsync();

        return Ok(new { id = model.Id, name = model.Name });
    }

    // ── Bearbeiten ────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var company = await _db.Companies.FindAsync(id);
        if (company is null) return NotFound();
        return View(company);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Company model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View(model);

        var existing = await _db.Companies.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Name     = model.Name;
        existing.Street   = model.Street;
        existing.ZipCode  = model.ZipCode;
        existing.City     = model.City;
        existing.Country  = model.Country;
        existing.Phone    = model.Phone;
        existing.Email    = model.Email;
        existing.Website  = model.Website;
        existing.Industry = model.Industry;
        existing.Notes    = model.Notes;
        existing.IsFavorite = model.IsFavorite;
        existing.UpdatedAt  = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Firma {existing.Name} wurde gespeichert.";
        return RedirectToAction("Details", new { id });
    }

    // ── Löschen ───────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var company = await _db.Companies
            .Include(c => c.Contacts.Where(ct => !ct.IsDeleted))
            .FirstOrDefaultAsync(c => c.Id == id);
        if (company is null) return NotFound();
        return View(company);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var company = await _db.Companies.FindAsync(id);
        if (company is null) return NotFound();

        company.IsDeleted  = true;
        company.UpdatedAt  = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Firma {company.Name} wurde gelöscht.";
        return RedirectToAction("Index");
    }

    // ── Favorit umschalten ────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFavorite(int id, string? returnUrl)
    {
        var company = await _db.Companies.FindAsync(id);
        if (company is null) return NotFound();

        company.IsFavorite = !company.IsFavorite;
        company.UpdatedAt  = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Details", new { id });
    }

    // ── CSV-Export ────────────────────────────────────────────────────────────

    public async Task<IActionResult> ExportCsv(string? q)
    {
        var query = _db.Companies.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c => c.Name.Contains(term));
        }
        var companies = await query.OrderBy(c => c.Name).ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Firmenname;Straße;PLZ;Ort;Land;Telefon;E-Mail;Website;Branche;Notizen");
        foreach (var c in companies)
        {
            sb.AppendLine(
                $"\"{Esc(c.Name)}\";\"{Esc(c.Street)}\";\"{Esc(c.ZipCode)}\";\"{Esc(c.City)}\";" +
                $"\"{Esc(c.Country)}\";\"{Esc(c.Phone)}\";\"{Esc(c.Email)}\";\"{Esc(c.Website)}\";" +
                $"\"{Esc(c.Industry)}\";\"{Esc(c.Notes)}\"");
        }

        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", "Firmen.csv");
    }

    private static string Esc(string? s) => (s ?? "").Replace("\"", "\"\"");
}
