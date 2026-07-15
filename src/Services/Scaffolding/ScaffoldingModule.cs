using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services.Companies.Contracts.Services;

namespace Services.Scaffolding;

public sealed class ScaffoldingModule : IServiceModule
{
    public ServiceManifest Manifest { get; } = ServiceManifest.Load(typeof(ScaffoldingModule).Assembly);
    public void Configure(IServiceCollection services) => services.AddSingleton<IServiceScaffolding, ServiceScaffolding>();

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/scaffolding/services", () => Results.Ok(ServiceScaffolder.Catalog)).RequireAuthorization();
        endpoints.MapPost("/api/scaffolding/services", async (HttpContext context, ScaffoldServiceRequest request) =>
        {
            if (context.Features.Get<IAntiforgeryValidationFeature>()?.IsValid == false) return Results.BadRequest();
            try
            {
                var result = await context.RequestServices.GetRequiredService<IServiceScaffolding>().Create(request.Name, request.Parent, context.RequestAborted);
                return !result.Loaded && result.Error is not null
                    ? Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity)
                    : Results.Created("/api/scaffolding/services", result);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DirectoryNotFoundException)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        }).RequireAuthorization("ManageUsers").WithMetadata(new RequireAntiforgeryTokenAttribute());
    }
}
