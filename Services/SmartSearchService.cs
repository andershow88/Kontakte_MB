using KontakteDB.Data;
using KontakteDB.Models;
using Microsoft.EntityFrameworkCore;

namespace KontakteDB.Services;

public class SmartSearchService
{
    private readonly AppDbContext _db;

    // BM25 parameters
    const double K1 = 1.4;
    const double B = 0.75;

    public SmartSearchService(AppDbContext db) => _db = db;

    public async Task<SmartSearchResult> SearchAsync(string query)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var rawTokens = Tokenize(query);
        if (rawTokens.Length == 0)
            return new SmartSearchResult();

        var expanded = ExpandWithSynonyms(rawTokens);

        var companies = await _db.Companies
            .Include(c => c.Contacts.Where(ct => !ct.IsDeleted))
            .ToListAsync();

        var contacts = await _db.Contacts
            .Include(c => c.Company)
            .ToListAsync();

        var companyDocs = companies.Select(c => new SearchDoc<Company>(c, BuildCompanyFields(c))).ToList();
        var contactDocs = contacts.Select(c => new SearchDoc<Contact>(c, BuildContactFields(c))).ToList();

        var companyResults = RankBm25(companyDocs, expanded, rawTokens)
            .Take(30)
            .Select(r => new ScoredCompany
            {
                Company = r.Doc.Entity,
                Score = Math.Round(r.Score, 2),
                MatchedFields = r.MatchedFields,
                MatchMethod = r.MatchMethod
            })
            .ToList();

        var contactResults = RankBm25(contactDocs, expanded, rawTokens)
            .Take(50)
            .Select(r => new ScoredContact
            {
                Contact = r.Doc.Entity,
                Score = Math.Round(r.Score, 2),
                MatchedFields = r.MatchedFields,
                MatchMethod = r.MatchMethod
            })
            .ToList();

        sw.Stop();

        return new SmartSearchResult
        {
            Companies = companyResults,
            Contacts = contactResults,
            ElapsedMs = sw.ElapsedMilliseconds,
            SearchTokens = rawTokens,
            ExpandedTokens = expanded.SelectMany(e => e.Variants).Distinct().ToArray()
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    // BM25F RANKING
    // ═══════════════════════════════════════════════════════════════════

    static List<RankedResult<T>> RankBm25<T>(List<SearchDoc<T>> docs, List<ExpandedToken> tokens, string[] rawTokens)
    {
        int N = docs.Count;
        if (N == 0) return new();

        var allFieldNames = docs.SelectMany(d => d.Fields.Select(f => f.Name)).Distinct().ToList();

        var avgFieldLengths = new Dictionary<string, double>();
        foreach (var fn in allFieldNames)
        {
            var lengths = docs.Select(d => d.Fields.FirstOrDefault(f => f.Name == fn)?.TokenCount ?? 0);
            avgFieldLengths[fn] = lengths.Average();
        }

        var docFreq = new Dictionary<string, int>();
        foreach (var et in tokens)
        {
            foreach (var variant in et.Variants)
            {
                if (docFreq.ContainsKey(variant)) continue;
                int count = docs.Count(d => d.Fields.Any(f => ContainsTerm(f.LowerValue, variant)));
                docFreq[variant] = count;
            }
        }

        var results = new List<RankedResult<T>>();

        foreach (var doc in docs)
        {
            double totalScore = 0;
            var matchedFields = new List<string>();
            var methods = new HashSet<string>();
            bool allTokensMatched = true;

            foreach (var et in tokens)
            {
                double bestTokenScore = 0;
                string? bestField = null;
                string? bestMethod = null;

                foreach (var field in doc.Fields)
                {
                    if (string.IsNullOrEmpty(field.LowerValue)) continue;

                    double avgDl = avgFieldLengths.GetValueOrDefault(field.Name, 1);

                    foreach (var variant in et.Variants)
                    {
                        int tf = CountTermFrequency(field.LowerValue, field.Words, variant);
                        if (tf == 0) continue;

                        int df = docFreq.GetValueOrDefault(variant, 0);
                        double idf = Math.Log((N - df + 0.5) / (df + 0.5) + 1.0);
                        double dl = field.TokenCount;
                        double norm = 1.0 - B + B * (dl / Math.Max(avgDl, 1));
                        double tfNorm = (tf * (K1 + 1.0)) / (tf + K1 * norm);
                        double fieldScore = idf * tfNorm * field.Weight;

                        string method = variant == et.Original ? "BM25" :
                            et.IsFuzzy(variant) ? "Fuzzy" : "Synonym";

                        if (method == "Synonym") fieldScore *= 0.85;
                        if (method == "Fuzzy") fieldScore *= 0.7;

                        if (fieldScore > bestTokenScore)
                        {
                            bestTokenScore = fieldScore;
                            bestField = field.Name;
                            bestMethod = method;
                        }
                    }
                }

                if (bestTokenScore > 0)
                {
                    totalScore += bestTokenScore;
                    if (bestField != null && !matchedFields.Contains(bestField))
                        matchedFields.Add(bestField);
                    if (bestMethod != null) methods.Add(bestMethod);
                }
                else
                {
                    var fuzzyResult = FuzzyFallback(doc, et.Original);
                    if (fuzzyResult.Score > 0)
                    {
                        double idf = Math.Log((N + 0.5) / 1.5);
                        totalScore += fuzzyResult.Score * idf * 0.5;
                        if (fuzzyResult.Field != null && !matchedFields.Contains(fuzzyResult.Field))
                            matchedFields.Add(fuzzyResult.Field);
                        methods.Add("Fuzzy");
                    }
                    else
                    {
                        allTokensMatched = false;
                    }
                }
            }

            if (!allTokensMatched && tokens.Count > 1)
                continue;

            if (totalScore > 0)
            {
                if (tokens.Count > 1 && allTokensMatched)
                    totalScore *= 1.0 + 0.1 * (tokens.Count - 1);

                results.Add(new RankedResult<T>
                {
                    Doc = doc,
                    Score = totalScore,
                    MatchedFields = matchedFields,
                    MatchMethod = string.Join(" + ", methods.OrderBy(m => m))
                });
            }
        }

        return results.OrderByDescending(r => r.Score).ToList();
    }

    static int CountTermFrequency(string lowerValue, string[] words, string term)
    {
        int count = 0;
        foreach (var w in words)
        {
            if (w == term || w.StartsWith(term))
                count++;
        }
        if (count == 0 && lowerValue.Contains(term))
            count = 1;
        return count;
    }

    static bool ContainsTerm(string lowerValue, string term)
    {
        if (string.IsNullOrEmpty(lowerValue)) return false;
        return lowerValue.Contains(term);
    }

    static (double Score, string? Field) FuzzyFallback<T>(SearchDoc<T> doc, string token)
    {
        double best = 0;
        string? bestField = null;

        foreach (var field in doc.Fields)
        {
            if (string.IsNullOrEmpty(field.LowerValue)) continue;

            foreach (var word in field.Words)
            {
                int dist = LevenshteinDistance(token,
                    word.Length > token.Length + 2 ? word[..(token.Length + 2)] : word);
                int threshold = token.Length <= 4 ? 1 : 2;
                if (dist <= threshold)
                {
                    double score = field.Weight * Math.Max(0.3, 0.6 - dist * 0.15);
                    if (score > best)
                    {
                        best = score;
                        bestField = field.Name;
                    }
                    break;
                }
            }

            if (best == 0 && token.Length >= 3)
            {
                double sim = TrigramSimilarity(token, field.LowerValue);
                if (sim >= 0.35)
                {
                    double score = field.Weight * sim * 0.4;
                    if (score > best)
                    {
                        best = score;
                        bestField = field.Name;
                    }
                }
            }
        }

        return (best, bestField);
    }

    // ═══════════════════════════════════════════════════════════════════
    // DOCUMENT INDEXING
    // ═══════════════════════════════════════════════════════════════════

    static List<SearchField> BuildCompanyFields(Company c) => new()
    {
        new("Name",     c.Name,     3.0),
        new("Ort",      c.City,     1.5),
        new("Land",     c.Country,  1.0),
        new("E-Mail",   c.Email,    2.0),
        new("Telefon",  c.Phone,    1.8),
        new("Branche",  c.Industry, 1.5),
        new("Straße",   c.Street,   1.0),
        new("PLZ",      c.ZipCode,  1.0),
        new("Website",  c.Website,  1.2),
        new("Notizen",  c.Notes,    0.6),
    };

    static List<SearchField> BuildContactFields(Contact c) => new()
    {
        new("Nachname",  c.LastName,      3.0),
        new("Vorname",   c.FirstName,     2.8),
        new("E-Mail",    c.Email,         2.0),
        new("Telefon",   c.Phone,         1.8),
        new("Mobil",     c.Mobile,        1.8),
        new("Position",  c.Position,      1.5),
        new("Abteilung", c.Department,    1.2),
        new("Ort",       c.City,          1.2),
        new("Land",      c.Country,       1.0),
        new("Firma",     c.Company?.Name, 2.0),
        new("Notizen",   c.Notes,         0.6),
    };

    // ═══════════════════════════════════════════════════════════════════
    // SYNONYM EXPANSION
    // ═══════════════════════════════════════════════════════════════════

    static readonly Dictionary<string, string[]> Synonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        // Kontaktdaten
        { "tel",        new[] { "telefon", "phone" } },
        { "telefon",    new[] { "tel", "phone" } },
        { "phone",      new[] { "telefon", "tel" } },
        { "handy",      new[] { "mobil", "mobile" } },
        { "mobil",      new[] { "handy", "mobile" } },
        { "mobile",     new[] { "handy", "mobil" } },
        { "mail",       new[] { "email", "e-mail" } },
        { "email",      new[] { "mail", "e-mail" } },
        { "fax",        new[] { "telefax" } },
        { "telefax",    new[] { "fax" } },

        // Adresse
        { "str",        new[] { "straße", "strasse" } },
        { "straße",     new[] { "str", "strasse" } },
        { "strasse",    new[] { "str", "straße" } },
        { "plz",        new[] { "postleitzahl" } },
        { "postleitzahl", new[] { "plz" } },

        // Städte
        { "münchen",    new[] { "munich", "muenchen" } },
        { "munich",     new[] { "münchen", "muenchen" } },
        { "muenchen",   new[] { "münchen", "munich" } },
        { "köln",       new[] { "cologne", "koeln" } },
        { "cologne",    new[] { "köln", "koeln" } },
        { "nürnberg",   new[] { "nuernberg", "nuremberg" } },
        { "nuremberg",  new[] { "nürnberg", "nuernberg" } },
        { "frankfurt",  new[] { "ffm", "fra" } },
        { "ffm",        new[] { "frankfurt" } },
        { "düsseldorf", new[] { "duesseldorf" } },
        { "duesseldorf",new[] { "düsseldorf" } },
        { "zürich",     new[] { "zurich", "zuerich" } },
        { "zurich",     new[] { "zürich", "zuerich" } },
        { "wien",       new[] { "vienna" } },
        { "vienna",     new[] { "wien" } },

        // Rechtsformen
        { "gmbh",       new[] { "gesellschaft" } },
        { "ag",         new[] { "aktiengesellschaft" } },

        // Positionen
        { "gf",         new[] { "geschäftsführer", "geschaeftsfuehrer" } },
        { "geschäftsführer", new[] { "gf", "geschaeftsfuehrer" } },
        { "ceo",        new[] { "geschäftsführer", "vorstand" } },
        { "cfo",        new[] { "finanzvorstand" } },
        { "cto",        new[] { "technikvorstand" } },
        { "vp",         new[] { "vice president" } },
        { "hr",         new[] { "personal", "human resources" } },
        { "it",         new[] { "informationstechnologie", "edv" } },
        { "edv",        new[] { "it", "informationstechnologie" } },

        // Länder
        { "de",         new[] { "deutschland", "germany" } },
        { "deutschland",new[] { "de", "germany" } },
        { "germany",    new[] { "de", "deutschland" } },
        { "at",         new[] { "österreich", "austria" } },
        { "österreich", new[] { "at", "austria" } },
        { "austria",    new[] { "at", "österreich" } },
        { "ch",         new[] { "schweiz", "switzerland" } },
        { "schweiz",    new[] { "ch", "switzerland" } },
    };

    static List<ExpandedToken> ExpandWithSynonyms(string[] tokens)
    {
        var result = new List<ExpandedToken>();
        foreach (var token in tokens)
        {
            var variants = new List<string> { token };
            var synonymVariants = new List<string>();

            if (Synonyms.TryGetValue(token, out var syns))
                synonymVariants.AddRange(syns);

            var umlautNorm = NormalizeUmlauts(token);
            if (umlautNorm != token)
            {
                variants.Add(umlautNorm);
                if (Synonyms.TryGetValue(umlautNorm, out var uSyns))
                    synonymVariants.AddRange(uSyns);
            }

            var umlautExpanded = ExpandUmlauts(token);
            if (umlautExpanded != token && umlautExpanded != umlautNorm)
                variants.Add(umlautExpanded);

            variants.AddRange(synonymVariants);
            result.Add(new ExpandedToken(token, variants.Distinct().ToList(), synonymVariants));
        }
        return result;
    }

    static string NormalizeUmlauts(string s) =>
        s.Replace("ae", "ä").Replace("oe", "ö").Replace("ue", "ü").Replace("ss", "ß");

    static string ExpandUmlauts(string s) =>
        s.Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");

    // ═══════════════════════════════════════════════════════════════════
    // TOKENIZER
    // ═══════════════════════════════════════════════════════════════════

    static string[] Tokenize(string query) =>
        query.ToLowerInvariant()
            .Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length >= 2)
            .ToArray();

    // ═══════════════════════════════════════════════════════════════════
    // FUZZY HELPERS
    // ═══════════════════════════════════════════════════════════════════

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

// ═══════════════════════════════════════════════════════════════════════
// DATA STRUCTURES
// ═══════════════════════════════════════════════════════════════════════

public class SearchField
{
    public string Name { get; }
    public string? Value { get; }
    public string LowerValue { get; }
    public string[] Words { get; }
    public int TokenCount { get; }
    public double Weight { get; }

    public SearchField(string name, string? value, double weight)
    {
        Name = name;
        Value = value;
        Weight = weight;
        LowerValue = (value ?? "").ToLowerInvariant();
        Words = LowerValue.Split(new[] { ' ', ',', '.', '-', '/', '@', '(', ')' },
            StringSplitOptions.RemoveEmptyEntries);
        TokenCount = Words.Length;
    }
}

public class SearchDoc<T>
{
    public T Entity { get; }
    public List<SearchField> Fields { get; }

    public SearchDoc(T entity, List<SearchField> fields)
    {
        Entity = entity;
        Fields = fields;
    }
}

public class ExpandedToken
{
    public string Original { get; }
    public List<string> Variants { get; }
    private readonly HashSet<string> _synonyms;

    public ExpandedToken(string original, List<string> variants, List<string> synonymVariants)
    {
        Original = original;
        Variants = variants;
        _synonyms = new HashSet<string>(synonymVariants, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsFuzzy(string variant) =>
        variant != Original && !_synonyms.Contains(variant);
}

public class RankedResult<T>
{
    public SearchDoc<T> Doc { get; set; } = null!;
    public double Score { get; set; }
    public List<string> MatchedFields { get; set; } = new();
    public string MatchMethod { get; set; } = "";
}

public class SmartSearchResult
{
    public List<ScoredCompany> Companies { get; set; } = new();
    public List<ScoredContact> Contacts { get; set; } = new();
    public long ElapsedMs { get; set; }
    public int TotalResults => Companies.Count + Contacts.Count;
    public string[] SearchTokens { get; set; } = Array.Empty<string>();
    public string[] ExpandedTokens { get; set; } = Array.Empty<string>();
}

public class ScoredCompany
{
    public Company Company { get; set; } = null!;
    public double Score { get; set; }
    public List<string> MatchedFields { get; set; } = new();
    public string MatchMethod { get; set; } = "";
}

public class ScoredContact
{
    public Contact Contact { get; set; } = null!;
    public double Score { get; set; }
    public List<string> MatchedFields { get; set; } = new();
    public string MatchMethod { get; set; } = "";
}
