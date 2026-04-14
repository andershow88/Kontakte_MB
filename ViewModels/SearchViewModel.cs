using KontakteDB.Models;

namespace KontakteDB.ViewModels;

public class SearchViewModel
{
    public string? Query { get; set; }
    public List<Company> Companies { get; set; } = new();
    public List<Contact> Contacts { get; set; } = new();
    public int TotalResults => Companies.Count + Contacts.Count;
    public bool HasResults => TotalResults > 0;
    public bool Searched { get; set; }
}
