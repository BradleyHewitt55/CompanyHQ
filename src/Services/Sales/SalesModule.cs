using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Services.Companies.Contracts.Services;

namespace Services.Sales;

public sealed class SalesModule : IServiceModule
{
    public ServiceManifest Manifest { get; } = ServiceManifest.Load(typeof(SalesModule).Assembly);
    public IReadOnlyList<ServiceComponentRegistration> Components => [new("index", typeof(Pages.SalesPage))];
    public void Configure(IServiceCollection services) { }
    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/api/services/sales", () => Results.Ok(new { service = "sales" }));
}
