namespace CompanyHQ.ServiceCatalog.Contracts;

public interface IServiceAvailability
{
    Task<ServiceCatalogDto?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ServiceCatalogDto>> ListAsync(CancellationToken cancellationToken = default);
    Task RequireAsync(string key, bool requireReady = false, CancellationToken cancellationToken = default);
}

/// <summary>Implemented by an always-enabled company core. Reads must observe committed state.</summary>
public interface IServiceSelectionReader
{
    Task<IReadOnlyDictionary<string, ServiceSelection>> ReadAsync(CancellationToken cancellationToken = default);
}
