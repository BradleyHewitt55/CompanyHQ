using Microsoft.EntityFrameworkCore;
using Services.Companies.Models;

namespace Services.Companies.Data;

public sealed class CompaniesDbContext(DbContextOptions<CompaniesDbContext> options) : DbContext(options)
{
    public DbSet<RegisteredCompany> Companies => Set<RegisteredCompany>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RegisteredCompany>().Ignore(company => company.Extensions);
        modelBuilder.Entity<RegisteredCompany>().HasIndex(company => company.Name).IsUnique();
    }
}
