using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services.Companies.Contracts.Services;

namespace Services.Scaffolding;

public sealed class ServiceScaffolding(IConfiguration configuration, IServiceProvider services) : IServiceScaffolding
{
    public IReadOnlyList<ServiceTemplateInfo> Templates => ServiceScaffolder.Catalog.Select(item => new ServiceTemplateInfo(item.Id, item.Name, item.Description)).ToArray();

    public async Task<ScaffoldedService> Create(string name, string parent, CancellationToken cancellationToken = default)
    {
        var root = configuration["Scaffolding:Root"] ?? Directory.GetCurrentDirectory();
        var directory = ServiceScaffolder.Generate(root, name, parent);
        var loader = services.GetService<IRuntimeServiceLoader>();
        var result = loader is null ? null : await loader.BuildAndLoad(directory, cancellationToken);
        return new(directory, result?.Loaded ?? false, result?.ServiceId, result?.Error);
    }
}
