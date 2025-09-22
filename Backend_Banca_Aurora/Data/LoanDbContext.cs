using Backend_Banca_Aurora.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend_Banca_Aurora.Data;

public class LoanDbContext : DbContext
{
    public LoanDbContext(DbContextOptions<LoanDbContext> options) : base(options) { }

    public DbSet<Customer> Customers { get; set; } = null!;
    public DbSet<LoanApplication> LoanApplications { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Customer>().HasIndex(x => x.FiscalCode).IsUnique();
        mb.Entity<Customer>().HasIndex(x => x.KeycloakUserId).IsUnique();
        mb.Entity<LoanApplication>()
          .HasOne(l => l.Customer)
          .WithMany()
          .HasForeignKey(l => l.CustomerId)
          .OnDelete(DeleteBehavior.Restrict);
    }
}
