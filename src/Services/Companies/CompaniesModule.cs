using Services.Companies.Contracts.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Services.Companies.Models;
using Services.Companies.Pages;
using Services.Companies.Data;
using Services.Companies.Contracts.Data;

namespace Services.Companies;

public sealed class CompaniesModule : IServiceModule
{
    public ServiceManifest Manifest { get; } = ServiceManifest.Load(typeof(CompaniesModule).Assembly);
    public IReadOnlyList<ServiceComponentRegistration> Components => [new("companies", typeof(CompaniesPage))];

    public void Configure(IServiceCollection services)
    {
        using var provider = services.BuildServiceProvider();
        var configuration = provider.GetService<Microsoft.Extensions.Configuration.IConfiguration>();
        var connection = configuration?.GetConnectionString("Registry") ?? "Data Source=companies.db";
        services.AddDbContextFactory<CompaniesDbContext>((serviceProvider, options) =>
        {
            var sqlite = serviceProvider.GetServices<IDbProvider>().Single(provider => provider.Name == "sqlite");
            sqlite.Configure(options, connection);
        });
        services.AddScoped<ICompanyRegistry, CompanyRegistry>();
        services.AddSingleton<ICompanyDeploymentManager, CompanyDeploymentManager>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/companies", async (HttpContext context, RegisteredCompany company, ICompanyRegistry registry) =>
        {
            var configuration = context.RequestServices.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
            if (!context.Request.Headers.TryGetValue("X-CompanyHQ-Token", out var token) || token != configuration["Registry:Token"])
                return Results.Unauthorized();
            try { return Results.Created($"/api/companies/{company.Id}", await registry.Register(company)); }
            catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
        }).AllowAnonymous();
        endpoints.MapPost("/api/companies/{id:guid}/delete", async (HttpContext context, Guid id, ICompanyRegistry registry, ICompanyDeploymentManager deployments, ILogger<CompaniesModule> logger) =>
        {
            if (context.Features.Get<Microsoft.AspNetCore.Antiforgery.IAntiforgeryValidationFeature>()?.IsValid == false) return Results.BadRequest();
            var confirmation = (await context.Request.ReadFormAsync(context.RequestAborted))["confirmation"].ToString();
            var company = await registry.GetCompany(id, context.RequestAborted);
            if (company is null) return Results.NotFound();
            if (!string.Equals(confirmation?.Trim(), company.Name, StringComparison.Ordinal))
                return Results.BadRequest(new { error = "Type the exact company name to confirm deletion." });
            try
            {
                await deployments.DeleteDeployment(company.Slug, context.RequestAborted);
                await registry.Delete(company, context.RequestAborted);
                logger.LogWarning("Company {CompanyId} ({CompanySlug}) deleted by {Actor}", company.Id, company.Slug, context.User.Identity?.Name);
                return Results.Redirect("/admin/companies?deleted=1");
            }
            catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
        }).RequireAuthorization("OwnerOnly").WithMetadata(new Microsoft.AspNetCore.Antiforgery.RequireAntiforgeryTokenAttribute());
    }
}
