using KontakteDB.Data;
using KontakteDB.Models;
using KontakteDB.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KontakteDB.Controllers;

[Authorize]
public class ContactsController : Controller
{
    private readonly AppDbContext _db;

    public ContactsController(AppDbContext db) => _db = db;

    // ── Liste ─────────────────────────────────────────────────────────────────

    public async Task<IActionResult> Index(
        string? q, int? companyId, string? city, string? country,
        bool? hasEmail, bool? hasPhone,
        string? sortBy, string? sortDir,
        int page = 1, int pageSize = 50)
    {
        var query = _db.Contacts.Include(c => c.Company).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c =>
                c.LastName.Contains(term) ||
                (c.FirstName != null && c.FirstName.Contains(term)) ||
                (c.Email != null && c.Email.Contains(term)) ||
                (c.Phone != null && c.Phone.Contains(term)) ||
                (c.Mobile != null && c.Mobile.Contains(term)) ||
                (c.Position != null && c.Position.Contains(term)) ||
                (c.City != null && c.City.Contains(term)) ||
                (c.Notes != null && c.Notes.Contains(term)) ||
                (c.Company != null && c.Company.Name.Contains(term)));
        }

        if (companyId.HasValue)
            query = query.Where(c => c.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(c => c.City == city);

        if (!string.IsNullOrWhiteSpace(country))
            query = query.Where(c => c.Country == country);

        if (hasEmail == true)
            query = query.Where(c => c.Email != null && c.Email != "");

        if (hasPhone == true)
            query = query.Where(c => c.Phone != null && c.Phone != "");

        query = (sortBy?.ToLower(), sortDir?.ToLower()) switch
        {
            ("lastname", "desc")  => query.OrderByDescending(c => c.LastName),
            ("firstname", "asc")  => query.OrderBy(c => c.FirstName),
            ("firstname", "desc") => query.OrderByDescending(c => c.FirstName),
            ("company", "asc")    => query.OrderBy(c => c.Company!.Name),
            ("company", "desc")   => query.OrderByDescending(c => c.Company!.Name),
            ("updated", "asc")    => query.OrderBy(c => c.UpdatedAt),
            ("updated", "desc")   => query.OrderByDescending(c => c.UpdatedAt),
            ("created", "asc")    => query.OrderBy(c => c.CreatedAt),
            ("created", "desc")   => query.OrderByDescending(c => c.CreatedAt),
            _                     => query.OrderBy(c => c.LastName)
        };

        var total = await query.CountAsync();
        var contacts = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var vm = new ContactListViewModel
        {
            Contacts      = contacts,
            SearchTerm    = q,
            FilterCompanyId = companyId,
            FilterCity    = city,
            FilterCountry = country,
            HasEmail      = hasEmail,
            HasPhone      = hasPhone,
            SortBy        = sortBy,
            SortDir       = sortDir,
            TotalCount    = total,
            Page          = page,
            PageSize      = pageSize,
            Companies     = await _db.Companies.OrderBy(c => c.Name)
                                .Select(c => new ValueTuple<int, string>(c.Id, c.Name))
                                .ToListAsync(),
            Cities        = await _db.Contacts.Where(c => c.City != null && c.City != "")
                                .Select(c => c.City!).Distinct().OrderBy(x => x).ToListAsync(),
            Countries     = await _db.Contacts.Where(c => c.Country != null && c.Country != "")
                                .Select(c => c.Country!).Distinct().OrderBy(x => x).ToListAsync()
        };

        return View(vm);
    }

    // ── Details ───────────────────────────────────────────────────────────────

    public async Task<IActionResult> Details(int id)
    {
        var contact = await _db.Contacts
            .Include(c => c.Company)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (contact is null) return NotFound();
        return View(contact);
    }

    // ── Anlegen ───────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Create(int? companyId)
    {
        await LoadCompanySelectList();
        return View(new Contact { CompanyId = companyId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Contact model)
    {
        // Dubletten-Prüfung (gleiche E-Mail)
        if (!string.IsNullOrWhiteSpace(model.Email))
        {
            var dupe = await _db.Contacts
                .FirstOrDefaultAsync(c => c.Email == model.Email);
            if (dupe is not null)
                ModelState.AddModelError("Email",
                    $"Ein Kontakt mit dieser E-Mail-Adresse existiert bereits ({dupe.DisplayName}).");
        }

        if (!ModelState.IsValid)
        {
            await LoadCompanySelectList();
            return View(model);
        }

        model.CreatedAt = DateTime.UtcNow;
        model.UpdatedAt = DateTime.UtcNow;
        _db.Contacts.Add(model);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Kontakt {model.DisplayName} wurde angelegt.";

        if (model.CompanyId.HasValue)
            return RedirectToAction("Details", "Companies", new { id = model.CompanyId });

        return RedirectToAction("Details", new { id = model.Id });
    }

    // ── Bearbeiten ────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var contact = await _db.Contacts.FindAsync(id);
        if (contact is null) return NotFound();
        await LoadCompanySelectList();
        return View(contact);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Contact model)
    {
        if (id != model.Id) return BadRequest();

        // Dubletten-Prüfung
        if (!string.IsNullOrWhiteSpace(model.Email))
        {
            var dupe = await _db.Contacts
                .FirstOrDefaultAsync(c => c.Email == model.Email && c.Id != id);
            if (dupe is not null)
                ModelState.AddModelError("Email",
                    $"Ein anderer Kontakt mit dieser E-Mail-Adresse existiert bereits ({dupe.DisplayName}).");
        }

        if (!ModelState.IsValid)
        {
            await LoadCompanySelectList();
            return View(model);
        }

        var existing = await _db.Contacts.FindAsync(id);
        if (existing is null) return NotFound();

        existing.CompanyId  = model.CompanyId;
        existing.Salutation = model.Salutation;
        existing.Title      = model.Title;
        existing.FirstName  = model.FirstName;
        existing.LastName   = model.LastName;
        existing.Position   = model.Position;
        existing.Department = model.Department;
        existing.Email      = model.Email;
        existing.Phone      = model.Phone;
        existing.Mobile     = model.Mobile;
        existing.Fax        = model.Fax;
        existing.Street     = model.Street;
        existing.ZipCode    = model.ZipCode;
        existing.City       = model.City;
        existing.Country    = model.Country;
        existing.Notes      = model.Notes;
        existing.IsFavorite = model.IsFavorite;
        existing.UpdatedAt  = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Kontakt {existing.DisplayName} wurde gespeichert.";
        return RedirectToAction("Details", new { id });
    }

    // ── Löschen ───────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var contact = await _db.Contacts
            .Include(c => c.Company)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (contact is null) return NotFound();
        return View(contact);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var contact = await _db.Contacts.FindAsync(id);
        if (contact is null) return NotFound();

        var companyId = contact.CompanyId;
        contact.IsDeleted = true;
        contact.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Kontakt {contact.DisplayName} wurde gelöscht.";

        if (companyId.HasValue)
            return RedirectToAction("Details", "Companies", new { id = companyId });

        return RedirectToAction("Index");
    }

    // ── Favorit umschalten ────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFavorite(int id, string? returnUrl)
    {
        var contact = await _db.Contacts.FindAsync(id);
        if (contact is null) return NotFound();

        contact.IsFavorite = !contact.IsFavorite;
        contact.UpdatedAt  = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Details", new { id });
    }

    // ── CSV-Export ────────────────────────────────────────────────────────────

    public async Task<IActionResult> ExportCsv(string? q, int? companyId)
    {
        var query = _db.Contacts.Include(c => c.Company).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c =>
                c.LastName.Contains(term) ||
                (c.FirstName != null && c.FirstName.Contains(term)));
        }
        if (companyId.HasValue)
            query = query.Where(c => c.CompanyId == companyId);

        var contacts = await query.OrderBy(c => c.LastName).ThenBy(c => c.FirstName).ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Anrede;Titel;Vorname;Nachname;Position;Abteilung;Firma;E-Mail;Telefon;Mobil;Fax;Straße;PLZ;Ort;Land;Notizen");
        foreach (var c in contacts)
        {
            sb.AppendLine(
                $"\"{Esc(c.Salutation)}\";\"{Esc(c.Title)}\";\"{Esc(c.FirstName)}\";\"{Esc(c.LastName)}\";" +
                $"\"{Esc(c.Position)}\";\"{Esc(c.Department)}\";\"{Esc(c.Company?.Name)}\";\"{Esc(c.Email)}\";" +
                $"\"{Esc(c.Phone)}\";\"{Esc(c.Mobile)}\";\"{Esc(c.Fax)}\";\"{Esc(c.Street)}\";" +
                $"\"{Esc(c.ZipCode)}\";\"{Esc(c.City)}\";\"{Esc(c.Country)}\";\"{Esc(c.Notes)}\"");
        }

        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", "Kontakte.csv");
    }

    // ── Hilfs-Methoden ────────────────────────────────────────────────────────

    private async Task LoadCompanySelectList()
    {
        var companies = await _db.Companies.OrderBy(c => c.Name)
            .Select(c => new SelectListItem(c.Name, c.Id.ToString()))
            .ToListAsync();
        companies.Insert(0, new SelectListItem("— Keine Firma —", ""));
        ViewBag.Companies = companies;
    }

    private static string Esc(string? s) => (s ?? "").Replace("\"", "\"\"");
}
