using KontakteDB.Models;

namespace KontakteDB.ViewModels;

public class ContactListViewModel
{
    public List<Contact> Contacts { get; set; } = new();
    public string? SearchTerm { get; set; }
    public int? FilterCompanyId { get; set; }
    public string? FilterCity { get; set; }
    public string? FilterCountry { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
    public bool? HasEmail { get; set; }
    public bool? HasPhone { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    public List<(int Id, string Name)> Companies { get; set; } = new();
    public List<string> Cities { get; set; } = new();
    public List<string> Countries { get; set; } = new();
}
