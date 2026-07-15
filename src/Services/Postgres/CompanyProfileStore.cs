using Microsoft.EntityFrameworkCore;
using Services.Companies.Data;
using Services.Companies.Models;

namespace Services.Postgres;

public sealed class CompanyProfileStore(IDbContextFactory<CompanyDbContext> factory) : ICompanyProfileStore
{
    public async Task<CompanyProfile?> Get(CancellationToken cancellationToken = default)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        return await database.Set<CompanyProfile>().AsNoTracking().SingleOrDefaultAsync(cancellationToken);
    }

    public async Task Save(CompanyProfile profile, CancellationToken cancellationToken = default)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        var profiles = database.Set<CompanyProfile>();
        var existing = await profiles.SingleOrDefaultAsync(cancellationToken);
        if (existing is null) profiles.Add(profile);
        else
        {
            existing.Name = profile.Name;
            existing.LogoUrl = profile.LogoUrl;
            existing.PrimaryColor = profile.PrimaryColor;
            existing.Email = profile.Email;
            existing.Phone = profile.Phone;
        }
        await database.SaveChangesAsync(cancellationToken);
    }
}
