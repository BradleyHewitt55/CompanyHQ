namespace Services.Companies.Contracts.Services;

public interface IServiceScaffolding
{
    IReadOnlyList<ServiceTemplateInfo> Templates { get; }
    Task<ScaffoldedService> Create(string name, string parent, CancellationToken cancellationToken = default);
}

public sealed record ServiceTemplateInfo(string Id, string Name, string Description);
public sealed record ScaffoldedService(string Directory, bool Loaded, string? ServiceId, string? Error);
