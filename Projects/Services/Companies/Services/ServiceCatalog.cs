using Abstractions.Companies;

namespace CompanyHQ.Services.Companies.Services;

// Mirrors CompanyRegistry: the filesystem is the catalogue, a directory under the
// company's Services folder IS a service.
public sealed class ServiceCatalog(IServerPaths paths, ICompanyContext companyContext)
    : IServiceCatalog
{
    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (companyContext.Current is null)
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var root = Path.Combine(paths.CompanyDirectory(companyContext.Current), "Services");
        if (!Directory.Exists(root))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        IReadOnlyList<string> names = Directory.EnumerateDirectories(root)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .Order()
            .ToList();
        return Task.FromResult(names);
    }
}
