using KontakteDB.Data;
using KontakteDB.Models;
using Microsoft.EntityFrameworkCore;

namespace KontakteDB.Services;

public class SmartSearchService
{
    private readonly AppDbContext _db;

    public SmartSearchService(AppDbContext db) => _db = db;

    public async Task<SmartSearchResult> SearchAsync(string query)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var tokens = Tokenize(query);
        if (tokens.Length == 0)
            return new SmartSearchResult();

        var companies = await _db.Companies
            .Include(c => c.Contacts.Where(ct => !ct.IsDeleted))
            .ToListAsync();

        var contacts = await _db.Contacts
            .Include(c => c.Company)
            .ToListAsync();

        var companyResults = companies
            .Select(c => ScoreCompany(c, tokens))
            .Where(r => r.Score > 0)
            .OrderByDescending(r => r.Score)
            .Take(30)
            .ToList();

        var contactResults = contacts
            .Select(c => ScoreContact(c, tokens))
            .Where(r => r.Score > 0)
            .OrderByDescending(r => r.Score)
            .Take(50)
            .ToList();

        sw.Stop();

        return new SmartSearchResult
        {
            Companies = companyResults,
            Contacts = contactResults,
            ElapsedMs = sw.ElapsedMilliseconds
        };
    }

    static string[] Tokenize(string query)
    {
        return query
            .ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length >= 2)
            .ToArray();
    }

    static ScoredCompany ScoreCompany(Company c, string[] tokens)
    {
        double total = 0;
        var matchedFields = new List<string>();

        foreach (var token in tokens)
        {
            double best = 0;
            string? bestField = null;

            best = TryScore(c.Name, token, 10.0, "Name", best, ref bestField);
            best = TryScore(c.City, token, 5.0, "Ort", best, ref bestField);
            best = TryScore(c.Country, token, 3.0, "Land", best, ref bestField);
            best = TryScore(c.Email, token, 7.0, "E-Mail", best, ref bestField);
            best = TryScore(c.Phone, token, 6.0, "Telefon", best, ref bestField);
            best = TryScore(c.Industry, token, 5.0, "Branche", best, ref bestField);
            best = TryScore(c.Street, token, 3.0, "Straße", best, ref bestField);
            best = TryScore(c.ZipCode, token, 3.0, "PLZ", best, ref bestField);
            best = TryScore(c.Website, token, 4.0, "Website", best, ref bestField);
            best = TryScore(c.Notes, token, 2.0, "Notizen", best, ref bestField);

            if (best <= 0)
                return new ScoredCompany { Company = c, Score = 0 };

            total += best;
            if (bestField != null && !matchedFields.Contains(bestField))
                matchedFields.Add(bestField);
        }

        return new ScoredCompany
        {
            Company = c,
            Score = Math.Round(total, 1),
            MatchedFields = matchedFields
        };
    }

    static ScoredContact ScoreContact(Contact c, string[] tokens)
    {
        double total = 0;
        var matchedFields = new List<string>();

        foreach (var token in tokens)
        {
            double best = 0;
            string? bestField = null;

            best = TryScore(c.LastName, token, 10.0, "Nachname", best, ref bestField);
            best = TryScore(c.FirstName, token, 9.0, "Vorname", best, ref bestField);
            best = TryScore(c.Email, token, 7.0, "E-Mail", best, ref bestField);
            best = TryScore(c.Phone, token, 6.0, "Telefon", best, ref bestField);
            best = TryScore(c.Mobile, token, 6.0, "Mobil", best, ref bestField);
            best = TryScore(c.Position, token, 5.0, "Position", best, ref bestField);
            best = TryScore(c.Department, token, 4.0, "Abteilung", best, ref bestField);
            best = TryScore(c.City, token, 4.0, "Ort", best, ref bestField);
            best = TryScore(c.Country, token, 3.0, "Land", best, ref bestField);
            best = TryScore(c.Company?.Name, token, 6.0, "Firma", best, ref bestField);
            best = TryScore(c.Notes, token, 2.0, "Notizen", best, ref bestField);

            if (best <= 0)
                return new ScoredContact { Contact = c, Score = 0 };

            total += best;
            if (bestField != null && !matchedFields.Contains(bestField))
                matchedFields.Add(bestField);
        }

        return new ScoredContact
        {
            Contact = c,
            Score = Math.Round(total, 1),
            MatchedFields = matchedFields
        };
    }

    static double TryScore(string? fieldValue, string token, double weight, string fieldName,
                           double currentBest, ref string? bestField)
    {
        if (string.IsNullOrEmpty(fieldValue)) return currentBest;

        var val = fieldValue.ToLowerInvariant();
        double score = 0;

        if (val == token)
            score = weight * 1.0;
        else if (val.StartsWith(token))
            score = weight * 0.9;
        else if (val.Contains(token))
            score = weight * 0.7;
        else
        {
            var words = val.Split(new[] { ' ', ',', '.', '-', '/', '@' },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                if (word.StartsWith(token))
                {
                    score = weight * 0.8;
                    break;
                }
            }

            if (score == 0)
            {
                int minLen = Math.Min(token.Length, 6);
                foreach (var word in words)
                {
                    int dist = LevenshteinDistance(token, word.Length > token.Length + 2
                        ? word[..(token.Length + 2)] : word);
                    int threshold = token.Length <= 4 ? 1 : 2;
                    if (dist <= threshold)
                    {
                        score = weight * Math.Max(0.3, 0.6 - dist * 0.15);
                        break;
                    }
                }
            }

            if (score == 0 && token.Length >= 3)
            {
                double sim = TrigramSimilarity(token, val);
                if (sim >= 0.3)
                    score = weight * sim * 0.5;
            }
        }

        if (score > currentBest)
        {
            bestField = fieldName;
            return score;
        }
        return currentBest;
    }

    static int LevenshteinDistance(string s, string t)
    {
        if (s.Length == 0) return t.Length;
        if (t.Length == 0) return s.Length;

        var d = new int[s.Length + 1, t.Length + 1];
        for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= t.Length; j++) d[0, j] = j;

        for (int i = 1; i <= s.Length; i++)
        for (int j = 1; j <= t.Length; j++)
        {
            int cost = s[i - 1] == t[j - 1] ? 0 : 1;
            d[i, j] = Math.Min(
                Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                d[i - 1, j - 1] + cost);
        }

        return d[s.Length, t.Length];
    }

    static double TrigramSimilarity(string a, string b)
    {
        var triA = GetTrigrams(a);
        var triB = GetTrigrams(b);
        if (triA.Count == 0 || triB.Count == 0) return 0;

        int common = triA.Intersect(triB).Count();
        return (double)common / Math.Max(triA.Count, triB.Count);
    }

    static HashSet<string> GetTrigrams(string s)
    {
        var set = new HashSet<string>();
        var padded = $"  {s} ";
        for (int i = 0; i <= padded.Length - 3; i++)
            set.Add(padded.Substring(i, 3));
        return set;
    }
}

public class SmartSearchResult
{
    public List<ScoredCompany> Companies { get; set; } = new();
    public List<ScoredContact> Contacts { get; set; } = new();
    public long ElapsedMs { get; set; }
    public int TotalResults => Companies.Count + Contacts.Count;
}

public class ScoredCompany
{
    public Company Company { get; set; } = null!;
    public double Score { get; set; }
    public List<string> MatchedFields { get; set; } = new();
}

public class ScoredContact
{
    public Contact Contact { get; set; } = null!;
    public double Score { get; set; }
    public List<string> MatchedFields { get; set; } = new();
}
