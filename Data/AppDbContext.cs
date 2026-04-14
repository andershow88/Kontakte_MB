using KontakteDB.Models;
using Microsoft.EntityFrameworkCore;

namespace KontakteDB.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Company>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).IsRequired().HasMaxLength(300);
            e.HasQueryFilter(c => !c.IsDeleted);
            e.HasIndex(c => c.Name);
            e.HasIndex(c => c.City);
        });

        modelBuilder.Entity<Contact>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.LastName).IsRequired().HasMaxLength(200);
            e.HasQueryFilter(c => !c.IsDeleted);
            e.HasOne(c => c.Company)
             .WithMany(co => co.Contacts)
             .HasForeignKey(c => c.CompanyId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(c => c.LastName);
            e.HasIndex(c => c.Email);
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Username).IsUnique();
        });
    }
}
