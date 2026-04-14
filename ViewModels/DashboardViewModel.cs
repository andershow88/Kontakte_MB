using KontakteDB.Models;

namespace KontakteDB.ViewModels;

public class DashboardViewModel
{
    public int TotalCompanies { get; set; }
    public int TotalContacts { get; set; }
    public int FavoriteCompanies { get; set; }
    public int FavoriteContacts { get; set; }
    public int ContactsWithEmail { get; set; }
    public int ContactsWithPhone { get; set; }

    public List<Company> RecentCompanies { get; set; } = new();
    public List<Contact> RecentContacts { get; set; } = new();
    public List<Company> FavoriteCompaniesList { get; set; } = new();
}
