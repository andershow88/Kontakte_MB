using System.ComponentModel.DataAnnotations;

namespace KontakteDB.Models;

public class Company
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Firmenname ist erforderlich.")]
    [MaxLength(300)]
    [Display(Name = "Firmenname")]
    public string Name { get; set; } = string.Empty;

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
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

    [MaxLength(200)]
    [EmailAddress(ErrorMessage = "Ungültige E-Mail-Adresse.")]
    [Display(Name = "E-Mail")]
    public string? Email { get; set; }

    [MaxLength(300)]
    [Display(Name = "Website")]
    public string? Website { get; set; }

    [MaxLength(100)]
    [Display(Name = "Branche")]
    public string? Industry { get; set; }

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
    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();

    // Computed helpers
    public string FullAddress => string.Join(", ",
        new[] { Street, ZipCode != null && City != null ? $"{ZipCode} {City}" : (ZipCode ?? City) }
        .Where(x => !string.IsNullOrWhiteSpace(x)));

    public int ContactCount => Contacts?.Count(c => !c.IsDeleted) ?? 0;
}
