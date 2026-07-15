using System.Linq.Expressions;
using Web.Admin.Components;
using Services.Companies.Contracts;
using Services.Companies.Contracts.Services;
using Web.Modules;
using Services.Orders;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Routing;

namespace Web.Tests;

public sealed class ModuleLoaderTests
{
    [Fact]
    public void LoadsModulesManifestsEndpointsAndRegistryPages()
    {
        var folder = CopyAssembly(typeof(OrdersModule).Assembly.Location, typeof(Services.Companies.CompaniesModule).Assembly.Location);
        try
        {
            var builder = WebApplication.CreateBuilder();
            builder.Services.AddScoped(typeof(IEntityService<>), typeof(StubEntityService<>));
            var registry = new ComponentRegistry(typeof(NotFoundMarker));
            var catalog = ModuleCatalog.Load(folder, builder.Services, registry, NullLogger.Instance);
            using var app = builder.Build();
            catalog.MapEndpoints(app);
            catalog.Modules.Should().Contain(module => module.Manifest.Id == "orders");
            catalog.Modules.Should().Contain(module => module.Manifest.Hidden);
            registry.Resolve("services/orders/index").Name.Should().Be("OrdersPage");
            ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).Select(endpoint => endpoint.DisplayName).Should().Contain(name => name!.Contains("/api/services/orders"));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public void AssemblyWithoutModuleIsSkippedWithoutCrashing()
    {
        var folder = CopyAssembly(typeof(FluentAssertions.AssertionExtensions).Assembly.Location);
        try
        {
            File.WriteAllText(Path.Combine(folder, "fluentassertions.json"), """{ "id": "fluentassertions", "name": "FluentAssertions", "version": "1.0.0" }""");
            var registry = new ComponentRegistry(typeof(NotFoundMarker));
            var catalog = ModuleCatalog.Load(folder, new ServiceCollection(), registry, NullLogger.Instance);
            catalog.Modules.Should().BeEmpty();
        }
        finally { Directory.Delete(folder, true); }
    }

    private static string CopyAssembly(params string[] sources)
    {
        var folder = Path.Combine(Path.GetTempPath(), "companyhq-modules-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        foreach (var source in sources)
        {
            File.Copy(source, Path.Combine(folder, Path.GetFileName(source)));
            var manifestName = Path.GetFileNameWithoutExtension(source).ToLowerInvariant() + ".json";
            var manifest = Path.Combine(Path.GetDirectoryName(source)!, manifestName);
            if (File.Exists(manifest)) File.Copy(manifest, Path.Combine(folder, manifestName));
        }
        return folder;
    }

    private sealed class StubEntityService<T> : IEntityService<T> where T : class, IEntityRecord
    {
        public Task<IReadOnlyList<T>> Get(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<T>>([]);
        public Task<T?> GetById(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<T?>(null);
        public Task<IReadOnlyList<T>> GetBy(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<T>>([]);
        public Task<T> Save(T entity, CancellationToken cancellationToken = default) => Task.FromResult(entity);
        public Task<bool> Delete(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
