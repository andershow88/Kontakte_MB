using System.ComponentModel.DataAnnotations;

namespace KontakteDB.Models;

public class Contact
{
    public int Id { get; set; }

    public int? CompanyId { get; set; }

    [MaxLength(50)]
    [Display(Name = "Anrede")]
    public string? Salutation { get; set; }

    [MaxLength(100)]
    [Display(Name = "Titel")]
    public string? Title { get; set; }

    [MaxLength(150)]
    [Display(Name = "Vorname")]
    public string? FirstName { get; set; }

    [Required(ErrorMessage = "Nachname ist erforderlich.")]
    [MaxLength(200)]
    [Display(Name = "Nachname")]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(200)]
    [Display(Name = "Position")]
    public string? Position { get; set; }

    [MaxLength(200)]
    [Display(Name = "Abteilung")]
    public string? Department { get; set; }

    [MaxLength(200)]
    [EmailAddress(ErrorMessage = "Ungültige E-Mail-Adresse.")]
    [Display(Name = "E-Mail")]
    public string? Email { get; set; }

    [MaxLength(100)]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

    [MaxLength(100)]
    [Display(Name = "Mobil")]
    public string? Mobile { get; set; }

    [MaxLength(100)]
    [Display(Name = "Fax")]
    public string? Fax { get; set; }

    [MaxLength(300)]
    [Display(Name = "Straße")]
    public string? Street { get; set; }

    [MaxLength(20)]
    [Display(Name = "PLZ")]
    public string? ZipCode { get; set; }

    [MaxLength(150)]
    [Display(Name = "Ort")]
    public string? City { get; set; }

    [MaxLength(100)]
    [Display(Name = "Land")]
    public string? Country { get; set; }

    [MaxLength(100)]
    [Display(Name = "Ansprechpartner-Typ")]
    public string? PreferredGreeting { get; set; }

    [MaxLength(2000)]
    [Display(Name = "Notizen")]
    public string? Notes { get; set; }

    [Display(Name = "Favorit")]
    public bool IsFavorite { get; set; }

    [Display(Name = "Erstellt am")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Display(Name = "Geändert am")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; }

    // Navigation
    public Company? Company { get; set; }

    // Computed helpers
    public string FullName => string.Join(" ",
        new[] { Salutation, Title, FirstName, LastName }
        .Where(x => !string.IsNullOrWhiteSpace(x)));

    public string DisplayName => string.IsNullOrWhiteSpace(FirstName)
        ? LastName
        : $"{FirstName} {LastName}";

    public string FullAddress => string.Join(", ",
        new[] { Street, ZipCode != null && City != null ? $"{ZipCode} {City}" : (ZipCode ?? City) }
        .Where(x => !string.IsNullOrWhiteSpace(x)));
}
