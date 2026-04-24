using KontakteDB.Models;
using KontakteDB.Services;

namespace KontakteDB.ViewModels;

public class SearchViewModel
{
    public string? Query { get; set; }
    public string Mode { get; set; } = "smart";
    public bool Searched { get; set; }

    // Smart Search results
    public List<ScoredCompany> ScoredCompanies { get; set; } = new();
    public List<ScoredContact> ScoredContacts { get; set; } = new();
    public long ElapsedMs { get; set; }
    public double MaxCompanyScore { get; set; }
    public double MaxContactScore { get; set; }
    public string[] SearchTokens { get; set; } = Array.Empty<string>();
    public string[] ExpandedTokens { get; set; } = Array.Empty<string>();

    // Legacy flat lists (kept for AI mode compatibility)
    public List<Company> Companies { get; set; } = new();
    public List<Contact> Contacts { get; set; } = new();

    public int TotalResults => Mode == "smart"
        ? ScoredCompanies.Count + ScoredContacts.Count
        : Companies.Count + Contacts.Count;
    public bool HasResults => TotalResults > 0;
}
