using System.ComponentModel.DataAnnotations;

namespace KontakteDB.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Benutzername ist erforderlich.")]
    [Display(Name = "Benutzername")]
    public string Benutzername { get; set; } = string.Empty;

    [Required(ErrorMessage = "Passwort ist erforderlich.")]
    [DataType(DataType.Password)]
    [Display(Name = "Passwort")]
    public string Passwort { get; set; } = string.Empty;

    [Display(Name = "Angemeldet bleiben")]
    public bool MerkenAuf { get; set; }

    public string? ReturnUrl { get; set; }
}
