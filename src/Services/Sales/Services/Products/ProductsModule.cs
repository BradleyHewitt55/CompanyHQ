using Services.Companies.Contracts.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
namespace Services.Products;
public sealed class ProductsModule : IServiceModule
{
    public ServiceManifest Manifest { get; } = ServiceManifest.Load(typeof(ProductsModule).Assembly);
    public IReadOnlyList<ServiceComponentRegistration> Components => [new("index", typeof(Pages.IndexPage))];
    public void Configure(IServiceCollection services) { }
    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/api/services/products", () => Results.Ok(new { service = "products" }));
}