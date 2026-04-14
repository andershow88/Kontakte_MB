using System.Text.Json;
using KontakteDB.Data;
using KontakteDB.Models;
using Microsoft.EntityFrameworkCore;

namespace KontakteDB.Services;

/// <summary>
/// Importiert Firmendaten und Kontakte aus Kontakte_MB.json in die Datenbank.
/// </summary>
public class ImportService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ImportService> _logger;
    private readonly IWebHostEnvironment _env;

    public ImportService(AppDbContext db, ILogger<ImportService> logger, IWebHostEnvironment env)
    {
        _db = db;
        _logger = logger;
        _env = env;
    }

    public async Task<ImportResult> RunImportAsync(string? jsonPath = null)
    {
        var result = new ImportResult();

        jsonPath ??= Path.Combine(_env.ContentRootPath, "Kontakte_MB.json");
        if (!File.Exists(jsonPath))
        {
            result.Error = $"JSON-Datei nicht gefunden: {jsonPath}";
            _logger.LogWarning(result.Error);
            return result;
        }

        JsonElement root;
        try
        {
            var json = await File.ReadAllTextAsync(jsonPath);
            root = JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch (Exception ex)
        {
            result.Error = $"JSON-Parsing-Fehler: {ex.Message}";
            _logger.LogError(ex, "JSON-Parsing-Fehler");
            return result;
        }

        if (!root.TryGetProperty("companies", out var companiesEl))
        {
            result.Error = "Kein 'companies'-Array in JSON gefunden.";
            return result;
        }

        foreach (var compEl in companiesEl.EnumerateArray())
        {
            try
            {
                var companyName = GetString(compEl, "company_name");
                if (string.IsNullOrWhiteSpace(companyName)) continue;

                // Primären Org-Datensatz lesen
                string? street = null, zip = null, city = null, country = null,
                        phone = null, email = null, website = null;

                if (compEl.TryGetProperty("organization_records", out var orgRecs) &&
                    orgRecs.GetArrayLength() > 0)
                {
                    var org = orgRecs[0];
                    phone = GetString(org, "main_phone");
                    email = GetString(org, "mail_address");

                    if (org.TryGetProperty("address", out var addr))
                    {
                        street  = GetString(addr, "street");
                        zip     = GetString(addr, "postal_code");
                        city    = GetString(addr, "city");
                        country = GetString(addr, "country");
                    }

                    // Website aus contact_methods extrahieren
                    if (org.TryGetProperty("contact_methods", out var cms))
                    {
                        foreach (var cm in cms.EnumerateArray())
                        {
                            var val = GetString(cm, "value") ?? "";
                            if (GetString(cm, "type") == "note" && val.StartsWith("Web ", StringComparison.OrdinalIgnoreCase))
                            {
                                website = val[4..].Trim();
                                break;
                            }
                        }
                    }
                }

                var company = new Company
                {
                    Name    = companyName.Trim(),
                    Street  = Truncate(street, 300),
                    ZipCode = Truncate(zip, 20),
                    City    = Truncate(city, 150),
                    Country = Truncate(country, 100),
                    Phone   = Truncate(phone, 100),
                    Email   = Truncate(email, 200),
                    Website = Truncate(website, 300),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _db.Companies.Add(company);
                await _db.SaveChangesAsync(); // ID erzeugen
                result.CompaniesImported++;

                // Kontakte importieren
                if (compEl.TryGetProperty("contacts", out var contacts))
                {
                    foreach (var ctEl in contacts.EnumerateArray())
                    {
                        try
                        {
                            var lastName = GetString(ctEl, "last_name_or_name_field")
                                        ?? GetString(ctEl, "display_name")
                                        ?? "Unbekannt";

                            var contact = new Contact
                            {
                                CompanyId  = company.Id,
                                Salutation = Truncate(GetString(ctEl, "salutation"), 50),
                                Title      = Truncate(GetString(ctEl, "title"), 100),
                                FirstName  = Truncate(GetString(ctEl, "first_name"), 150),
                                LastName   = Truncate(lastName, 200) ?? "Unbekannt",
                                Position   = Truncate(GetString(ctEl, "position"), 200),
                                Department = Truncate(GetString(ctEl, "department"), 200),
                                Email      = Truncate(GetString(ctEl, "mail_address"), 200),
                                Phone      = Truncate(GetString(ctEl, "main_phone"), 100),
                                Fax        = Truncate(GetString(ctEl, "fax"), 100),
                                Mobile     = Truncate(GetString(ctEl, "mobile"), 100),
                                PreferredGreeting = Truncate(GetString(ctEl, "preferred_greeting"), 100),
                                CreatedAt  = DateTime.UtcNow,
                                UpdatedAt  = DateTime.UtcNow
                            };

                            // Mobile aus contact_methods, falls nicht direkt vorhanden
                            if (string.IsNullOrWhiteSpace(contact.Mobile) &&
                                ctEl.TryGetProperty("contact_methods", out var ccms))
                            {
                                foreach (var cm in ccms.EnumerateArray())
                                {
                                    var ctx = GetString(cm, "context") ?? "";
                                    var typ = GetString(cm, "type") ?? "";
                                    if (typ == "phone" &&
                                        (ctx.Contains("mobil", StringComparison.OrdinalIgnoreCase) ||
                                         ctx.Contains("handy", StringComparison.OrdinalIgnoreCase)))
                                    {
                                        contact.Mobile = Truncate(GetString(cm, "value"), 100);
                                        break;
                                    }
                                }
                            }

                            // Adresse vom Kontakt (falls vorhanden, sonst von Firma übernehmen)
                            if (ctEl.TryGetProperty("address", out var cAddr))
                            {
                                contact.Street  = Truncate(GetString(cAddr, "street") ?? street, 300);
                                contact.ZipCode = Truncate(GetString(cAddr, "postal_code") ?? zip, 20);
                                contact.City    = Truncate(GetString(cAddr, "city") ?? city, 150);
                                contact.Country = Truncate(GetString(cAddr, "country") ?? country, 100);
                            }
                            else
                            {
                                contact.Street  = Truncate(street, 300);
                                contact.ZipCode = Truncate(zip, 20);
                                contact.City    = Truncate(city, 150);
                                contact.Country = Truncate(country, 100);
                            }

                            _db.Contacts.Add(contact);
                            result.ContactsImported++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Kontakt-Import übersprungen in Firma {Company}", companyName);
                            result.Errors++;
                        }
                    }

                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Firma-Import übersprungen");
                result.Errors++;
            }
        }

        // standalone_contacts verarbeiten
        if (root.TryGetProperty("standalone_contacts", out var standalone))
        {
            foreach (var ctEl in standalone.EnumerateArray())
            {
                try
                {
                    var lastName = GetString(ctEl, "last_name_or_name_field")
                                ?? GetString(ctEl, "display_name")
                                ?? "Unbekannt";

                    var contact = new Contact
                    {
                        CompanyId  = null,
                        Salutation = Truncate(GetString(ctEl, "salutation"), 50),
                        FirstName  = Truncate(GetString(ctEl, "first_name"), 150),
                        LastName   = Truncate(lastName, 200) ?? "Unbekannt",
                        Position   = Truncate(GetString(ctEl, "position"), 200),
                        Department = Truncate(GetString(ctEl, "department"), 200),
                        Email      = Truncate(GetString(ctEl, "mail_address"), 200),
                        Phone      = Truncate(GetString(ctEl, "main_phone"), 100),
                        CreatedAt  = DateTime.UtcNow,
                        UpdatedAt  = DateTime.UtcNow
                    };

                    _db.Contacts.Add(contact);
                    result.ContactsImported++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Standalone-Kontakt-Import übersprungen");
                    result.Errors++;
                }
            }
            await _db.SaveChangesAsync();
        }

        _logger.LogInformation("Import abgeschlossen: {C} Firmen, {K} Kontakte, {E} Fehler",
            result.CompaniesImported, result.ContactsImported, result.Errors);

        return result;
    }

    private static string? GetString(JsonElement el, string key)
    {
        if (el.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.String)
            return val.GetString();
        return null;
    }

    private static string? Truncate(string? s, int max) =>
        s == null ? null : (s.Length > max ? s[..max] : s);
}

public class ImportResult
{
    public int CompaniesImported { get; set; }
    public int ContactsImported { get; set; }
    public int Errors { get; set; }
    public string? Error { get; set; }
    public bool Success => Error == null;
}
