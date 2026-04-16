using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using KontakteDB.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KontakteDB.Controllers;

[Authorize]
public class AiAssistentController : Controller
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public AiAssistentController(
        AppDbContext db,
        IConfiguration config,
        IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost]
    public async Task<IActionResult> Fragen([FromBody] ChatAnfrageDto anfrage)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? _config["OpenAiApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
            return Json(new { error = "Kein OPENAI_API_KEY konfiguriert. Bitte in Railway-Umgebungsvariablen eintragen." });

        if (string.IsNullOrWhiteSpace(anfrage?.Frage))
            return Json(new { error = "Keine Frage angegeben." });

        try
        {
            // ── Datenbankkontext aufbauen ────────────────────────────────────
            var kontext = await BaueKontextAsync(anfrage.Frage);

            // ── Nachrichtenverlauf aufbauen ──────────────────────────────────
            var messages = new List<object>();

            foreach (var msg in anfrage.Verlauf ?? [])
                messages.Add(new { role = msg.Rolle, content = msg.Text });

            var userContent = string.IsNullOrWhiteSpace(kontext)
                ? anfrage.Frage
                : $"{anfrage.Frage}\n\n---\n*Aktuelle Systemdaten (automatisch geladen):*\n{kontext}";

            messages.Add(new { role = "user", content = userContent });

            // ── System-Prompt ────────────────────────────────────────────────
            const string systemPrompt = """
                Du bist der KI-Assistent der Kontaktdatenbank der Merkur Privatbank KGaA.
                Du hilfst Mitarbeitern mit Fragen zur Software, zur Handhabung und zu konkreten Kontakt- und Firmendaten.

                **Software-Übersicht:**
                Die Kontaktdatenbank verwaltet Firmen (Companies) und Kontakte (Contacts) der Merkur Privatbank.
                Firmen können mehrere Kontaktpersonen haben. Jeder Kontakt ist optional einer Firma zugeordnet.
                Kontakte und Firmen können als Favoriten markiert werden und besitzen Felder wie
                Anschrift, Telefonnummern, E-Mail, Position, Branche und Notizen.

                **Wichtige Felder einer Firma:**
                Name, Straße, PLZ, Ort, Land, Telefon, E-Mail, Website, Branche, Notizen, Favorit, Erstellt/Geändert am.

                **Wichtige Felder eines Kontakts:**
                Anrede, Titel, Vor-/Nachname, Position, Abteilung, E-Mail, Telefon, Mobil, Fax,
                Anschrift, Firma (Zuordnung), Notizen, Favorit.

                **Bereiche der Software:**
                - Dashboard: Übersicht (Anzahl Firmen, Kontakte, Favoriten, zuletzt geänderte Datensätze)
                - Firmen: Liste, Detailansicht mit zugeordneten Kontakten, Import/Export, Favoriten
                - Kontakte: Liste, Detailansicht, Filter nach Firma, Favoriten
                - Suche: Volltextsuche über Firmen und Kontakte
                - Administration: Nur für Rolle "Admin" – Benutzerverwaltung

                **Rollen:** Admin (Vollzugriff), weitere Rollen sind konfigurierbar.

                Antworte immer auf Deutsch. Sei präzise, freundlich und strukturiert.
                Nutze Markdown für Listen und Hervorhebungen.
                Bei Listen (Firmen/Kontakte): zeige Name, wichtigste Felder, ggf. Firmenbezug.
                Wenn du keine Daten zu einer spezifischen Anfrage hast, sage es klar.
                Beantworte ausschließlich Fragen, die sich auf diese Software oder ihre Daten beziehen.
                """;

            // System-Prompt als erstes Element in messages einfügen
            messages.Insert(0, new { role = "system", content = systemPrompt });

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var requestBody = new
            {
                model = "gpt-4o-mini",
                max_tokens = 1500,
                messages
            };

            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(requestBody, jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                Console.Error.WriteLine($"[AI] OpenAI error {response.StatusCode}: {body}");
                return Json(new { error = $"API-Fehler ({response.StatusCode}). Bitte API-Key prüfen." });
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            var result = await JsonSerializer.DeserializeAsync<OpenAiResponse>(stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var antwort = result?.Choices?.FirstOrDefault()?.Message?.Content ?? "Keine Antwort erhalten.";
            return Json(new { antwort });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[AI] Exception: {ex.Message}");
            return Json(new { error = "Interner Fehler beim Aufrufen des KI-Assistenten." });
        }
    }

    // ── Datenbankkontext aufbauen ────────────────────────────────────────────

    private async Task<string> BaueKontextAsync(string frage)
    {
        var sb = new StringBuilder();
        var frageLower = frage.ToLowerInvariant();

        // ── Immer: Statistik ─────────────────────────────────────────────────
        var firmenAnzahl = await _db.Companies.CountAsync();
        var kontakteAnzahl = await _db.Contacts.CountAsync();
        var favoritenFirmen = await _db.Companies.CountAsync(c => c.IsFavorite);
        var favoritenKontakte = await _db.Contacts.CountAsync(c => c.IsFavorite);

        sb.AppendLine("**Bestand (aktuell):**");
        sb.AppendLine($"- Firmen: {firmenAnzahl} (davon Favoriten: {favoritenFirmen})");
        sb.AppendLine($"- Kontakte: {kontakteAnzahl} (davon Favoriten: {favoritenKontakte})");
        sb.AppendLine();

        // ── Stopwörter für die Volltextsuche ─────────────────────────────────
        var stoppwoerter = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "wer","was","wie","wo","wann","warum","welche","welcher","welches",
            "ist","sind","hat","hatte","haben","war","wird","kann",
            "der","die","das","den","dem","des","ein","eine","einer","einen",
            "und","oder","aber","auch","noch","nur","mit","von","zu","zur","zum",
            "im","in","am","an","auf","aus","bei","bis","für","über","unter",
            "mir","mich","dir","dich","uns","euch","sich","ihr","ihn",
            "zeig","zeige","zeigen","liste","listen","finde","findet","finden",
            "such","suche","suchen","gib","gibt","geb","nenn","nenne","nennen",
            "mal","bitte","jetzt","doch","noch","schon","sehr","ganz","alle",
            "kontakt","kontakte","kontakts","firma","firmen","person","personen",
            "ansprechpartner","mitarbeiter","unternehmen","company","name","namen",
            "telefon","telefonnummer","mobil","handy","mail","email","e-mail",
            "adresse","anschrift","straße","ort","stadt","plz","land","position",
            "abteilung","branche","favorit","favoriten","website","notiz","notizen"
        };

        // Suchbegriffe aus Frage extrahieren (Wörter > 2 Zeichen, keine Stopwörter)
        var suchBegriffe = Regex.Matches(frage, @"[\p{L}][\p{L}\-]{2,}")
            .Select(m => m.Value)
            .Where(w => !stoppwoerter.Contains(w))
            .Distinct()
            .Take(5)
            .ToList();

        // E-Mail- und Telefon-Muster direkt extrahieren
        var emailMatch = Regex.Match(frage, @"[\w\.\-]+@[\w\.\-]+\.\w{2,}");
        var phoneMatch = Regex.Match(frage, @"\+?\d[\d\s\-\/\(\)]{6,}\d");

        bool sucheFirmen = frageLower.Contains("firma") || frageLower.Contains("firmen")
            || frageLower.Contains("unternehmen") || frageLower.Contains("company");
        bool sucheKontakte = frageLower.Contains("kontakt") || frageLower.Contains("person")
            || frageLower.Contains("ansprechpartner") || frageLower.Contains("mitarbeiter")
            || frageLower.Contains("telefon") || frageLower.Contains("mobil")
            || frageLower.Contains("mail") || frageLower.Contains("e-mail");
        bool sucheListe = frageLower.Contains("liste") || frageLower.Contains("zeig")
            || frageLower.Contains("aktuell")
            || frageLower.Contains("neueste") || frageLower.Contains("letzte");
        bool sucheFavoriten = frageLower.Contains("favorit");
        bool sucheBranche = frageLower.Contains("branche");

        // ── Gezielte Suche über extrahierte Begriffe ─────────────────────────
        bool trefferGefunden = false;

        if (suchBegriffe.Any() || emailMatch.Success || phoneMatch.Success)
        {
            foreach (var begriff in suchBegriffe.Concat(new[] {
                emailMatch.Success ? emailMatch.Value : null,
                phoneMatch.Success ? phoneMatch.Value.Replace(" ", "").Replace("-", "").Replace("/", "") : null
            }).Where(b => !string.IsNullOrEmpty(b)).Take(5))
            {
                var term = begriff!;

                // Kontakt-Treffer (Name, E-Mail, Telefon, Mobil, Position, Notizen)
                var kontaktTreffer = await _db.Contacts
                    .Include(c => c.Company)
                    .Where(c =>
                        c.LastName.Contains(term) ||
                        (c.FirstName != null && c.FirstName.Contains(term)) ||
                        (c.Email != null && c.Email.Contains(term)) ||
                        (c.Phone != null && c.Phone.Contains(term)) ||
                        (c.Mobile != null && c.Mobile.Contains(term)) ||
                        (c.Position != null && c.Position.Contains(term)) ||
                        (c.Department != null && c.Department.Contains(term)) ||
                        (c.Notes != null && c.Notes.Contains(term)))
                    .OrderBy(c => c.LastName)
                    .Take(8)
                    .ToListAsync();

                // Firmen-Treffer (Name, Ort, Branche, Mail, Telefon, Notizen)
                var firmenTreffer = await _db.Companies
                    .Where(c =>
                        c.Name.Contains(term) ||
                        (c.City != null && c.City.Contains(term)) ||
                        (c.Industry != null && c.Industry.Contains(term)) ||
                        (c.Email != null && c.Email.Contains(term)) ||
                        (c.Phone != null && c.Phone.Contains(term)) ||
                        (c.Notes != null && c.Notes.Contains(term)))
                    .OrderBy(c => c.Name)
                    .Take(8)
                    .ToListAsync();

                if (kontaktTreffer.Any() || firmenTreffer.Any())
                {
                    trefferGefunden = true;
                    sb.AppendLine($"**Treffer für \"{term}\":**");

                    foreach (var k in kontaktTreffer)
                    {
                        var fav = k.IsFavorite ? "★ " : "";
                        sb.AppendLine($"- {fav}**{k.DisplayName}**"
                            + (k.Position != null ? $" – {k.Position}" : "")
                            + (k.Company != null ? $" @ {k.Company.Name}" : "")
                            + (k.Department != null ? $" ({k.Department})" : ""));
                        if (!string.IsNullOrWhiteSpace(k.Email))  sb.AppendLine($"  📧 {k.Email}");
                        if (!string.IsNullOrWhiteSpace(k.Phone))   sb.AppendLine($"  ☎ Telefon: {k.Phone}");
                        if (!string.IsNullOrWhiteSpace(k.Mobile))  sb.AppendLine($"  📱 Mobil: {k.Mobile}");
                        if (!string.IsNullOrWhiteSpace(k.Fax))     sb.AppendLine($"  📠 Fax: {k.Fax}");
                        var adr = k.FullAddress;
                        if (!string.IsNullOrWhiteSpace(adr))       sb.AppendLine($"  📍 {adr}");
                    }

                    foreach (var f in firmenTreffer)
                    {
                        var fav = f.IsFavorite ? "★ " : "";
                        sb.AppendLine($"- {fav}**{f.Name}** (Firma)"
                            + (f.Industry != null ? $" – {f.Industry}" : ""));
                        if (!string.IsNullOrWhiteSpace(f.Email))   sb.AppendLine($"  📧 {f.Email}");
                        if (!string.IsNullOrWhiteSpace(f.Phone))   sb.AppendLine($"  ☎ {f.Phone}");
                        if (!string.IsNullOrWhiteSpace(f.Website)) sb.AppendLine($"  🌐 {f.Website}");
                        var adr = f.FullAddress;
                        if (!string.IsNullOrWhiteSpace(adr))       sb.AppendLine($"  📍 {adr}");
                    }
                    sb.AppendLine();
                }
            }
        }

        // ── Fallback: aktuelle Listen bei generischen Fragen ─────────────────
        if (!trefferGefunden && (sucheFirmen || (sucheListe && !sucheKontakte)))
        {
            var firmen = await _db.Companies
                .OrderByDescending(c => c.UpdatedAt)
                .Take(15)
                .ToListAsync();

            sb.AppendLine("**Firmen (zuletzt geändert, max. 15):**");
            foreach (var f in firmen)
            {
                var fav = f.IsFavorite ? "★ " : "";
                var ort = !string.IsNullOrWhiteSpace(f.City) ? f.City : "–";
                var branche = !string.IsNullOrWhiteSpace(f.Industry) ? f.Industry : "–";
                sb.AppendLine($"- {fav}{f.Name} | Ort: {ort} | Branche: {branche} | Tel: {f.Phone ?? "–"}");
            }
            sb.AppendLine();
        }

        if (!trefferGefunden && (sucheKontakte || (sucheListe && !sucheFirmen)))
        {
            var kontakte = await _db.Contacts
                .Include(c => c.Company)
                .OrderByDescending(c => c.UpdatedAt)
                .Take(15)
                .ToListAsync();

            sb.AppendLine("**Kontakte (zuletzt geändert, max. 15):**");
            foreach (var k in kontakte)
            {
                var fav = k.IsFavorite ? "★ " : "";
                sb.AppendLine($"- {fav}{k.DisplayName}"
                    + (k.Company != null ? $" @ {k.Company.Name}" : "")
                    + (k.Position != null ? $" – {k.Position}" : "")
                    + $" | Tel: {k.Phone ?? k.Mobile ?? "–"} | E-Mail: {k.Email ?? "–"}");
            }
            sb.AppendLine();
        }

        // ── Favoriten ────────────────────────────────────────────────────────
        if (sucheFavoriten)
        {
            var favFirmen = await _db.Companies
                .Where(c => c.IsFavorite)
                .OrderBy(c => c.Name)
                .Take(20)
                .ToListAsync();

            var favKontakte = await _db.Contacts
                .Include(c => c.Company)
                .Where(c => c.IsFavorite)
                .OrderBy(c => c.LastName)
                .Take(20)
                .ToListAsync();

            if (favFirmen.Any())
            {
                sb.AppendLine("**Favorisierte Firmen:**");
                foreach (var f in favFirmen)
                    sb.AppendLine($"- {f.Name} ({f.City ?? "–"}, {f.Industry ?? "–"}) | Tel: {f.Phone ?? "–"}");
                sb.AppendLine();
            }

            if (favKontakte.Any())
            {
                sb.AppendLine("**Favorisierte Kontakte:**");
                foreach (var k in favKontakte)
                    sb.AppendLine($"- {k.DisplayName} ({k.Company?.Name ?? "–"}) | Tel: {k.Phone ?? k.Mobile ?? "–"} | E-Mail: {k.Email ?? "–"}");
                sb.AppendLine();
            }
        }

        // ── Branchen-Übersicht ───────────────────────────────────────────────
        if (sucheBranche)
        {
            var branchen = await _db.Companies
                .Where(c => c.Industry != null && c.Industry != "")
                .GroupBy(c => c.Industry)
                .Select(g => new { Branche = g.Key!, Anzahl = g.Count() })
                .OrderByDescending(b => b.Anzahl)
                .Take(10)
                .ToListAsync();

            if (branchen.Any())
            {
                sb.AppendLine("**Branchen-Verteilung:**");
                foreach (var b in branchen)
                    sb.AppendLine($"- {b.Branche}: {b.Anzahl} Firmen");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record ChatAnfrageDto(string Frage, List<ChatNachrichtDto>? Verlauf);
public record ChatNachrichtDto(string Rolle, string Text);

internal class OpenAiResponse
{
    public List<OpenAiChoice>? Choices { get; set; }
}
internal class OpenAiChoice
{
    public OpenAiMessage? Message { get; set; }
}
internal class OpenAiMessage
{
    public string? Content { get; set; }
}
