using Abstractions.Companies;
using Abstractions.Data;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Services.Companies.Services;

// The filesystem is the registry: a directory under the server root IS a registered
// company. No separate index to drift out of sync with what's actually on disk.
public sealed class CompanyRegistry(IServerPaths paths, IServiceProvider serviceProvider) : ICompanyRegistry
{
    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(paths.CompaniesRoot))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        IReadOnlyList<string> names = Directory.EnumerateDirectories(paths.CompaniesRoot)
            .Select(Path.GetFileName)
            .Where(n => n is not null)
            .Select(n => n!)
            .Order()
            .ToList();
        return Task.FromResult(names);
    }

    public async Task RegisterAsync(
        string companyName,
        string? externalConnectionString = null,
        CancellationToken cancellationToken = default)
    {
        NameValidator.Validate(companyName, nameof(companyName), "Company");
        var companyDirectory = paths.CompanyDirectory(companyName);
        Directory.CreateDirectory(companyDirectory);
        Directory.CreateDirectory(Path.Combine(companyDirectory, "Services"));

        // The creation workflow normally provisions after registration; the optional
        // string keeps registration usable for scripted hosts that already chose a store.
        if (externalConnectionString is not null)
        {
            var databases = serviceProvider.GetService<ICompanyDatabases>()
                ?? throw new InvalidOperationException(
                    "A connection string was provided but no database provider plugin is loaded.");
            await databases.EnsureDatabaseAsync(companyName, externalConnectionString, cancellationToken);
        }
    }

}
