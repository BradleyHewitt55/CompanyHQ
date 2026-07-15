using Microsoft.EntityFrameworkCore;

namespace Services.Companies.Data;

using Services.Companies.Models;

public interface ICompanyRegistry
{
    Task<IReadOnlyList<RegisteredCompany>> GetCompanies(CancellationToken cancellationToken = default);
    Task<RegisteredCompany?> GetCompany(Guid id, CancellationToken cancellationToken = default);
    Task<RegisteredCompany> Register(RegisteredCompany company, CancellationToken cancellationToken = default);
    Task Delete(RegisteredCompany company, CancellationToken cancellationToken = default);
}

public sealed class CompanyRegistry(IDbContextFactory<CompaniesDbContext> factory) : ICompanyRegistry
{
    public async Task<IReadOnlyList<RegisteredCompany>> GetCompanies(CancellationToken cancellationToken = default)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        return await database.Companies.AsNoTracking().OrderBy(company => company.Name).ToArrayAsync(cancellationToken);
    }

    public async Task<RegisteredCompany> Register(RegisteredCompany company, CancellationToken cancellationToken = default)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        if (!CompanyDeploymentManager.IsValidSlug(company.Slug))
            throw new InvalidOperationException("Company slug is invalid.");
        if (await database.Companies.AnyAsync(item => item.Name == company.Name, cancellationToken))
            throw new InvalidOperationException($"A company named '{company.Name}' is already registered.");
        database.Companies.Add(company);
        await database.SaveChangesAsync(cancellationToken);
        return company;
    }

    public async Task<RegisteredCompany?> GetCompany(Guid id, CancellationToken cancellationToken = default)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        return await database.Companies.SingleOrDefaultAsync(company => company.Id == id, cancellationToken);
    }

    public async Task Delete(RegisteredCompany company, CancellationToken cancellationToken = default)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        database.Companies.Remove(company);
        await database.SaveChangesAsync(cancellationToken);
    }
}
