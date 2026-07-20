namespace Abstractions.Companies;

// Listing a company's services is a service, not something each consumer reimplements
// by walking the filesystem: the admin explorer, the CLI, and any future host all read
// the same catalogue through this contract.
public interface IServiceCatalog
{
    // Services belonging to the ambient company. Empty when no company is selected,
    // so callers can render without a null check on ICompanyContext.
    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default);
}
