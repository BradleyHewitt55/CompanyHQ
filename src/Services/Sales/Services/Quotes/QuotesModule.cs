using Services.Companies.Contracts.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
namespace Services.Quotes;
public sealed class QuotesModule : IServiceModule
{
    public ServiceManifest Manifest { get; } = ServiceManifest.Load(typeof(QuotesModule).Assembly);
    public IReadOnlyList<ServiceComponentRegistration> Components => [new("index", typeof(Pages.IndexPage))];
    public void Configure(IServiceCollection services) { }
    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/api/services/quotes", () => Results.Ok(new { service = "quotes" }));
}