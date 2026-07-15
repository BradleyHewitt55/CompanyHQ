using System.Text.Json;
using Services.Companies.Contracts.Data;
using Services.Companies.Contracts.Metadata;
using Services.Auth.Models;
using Services.Companies.Contracts.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services.Postgres.Providers;
using Microsoft.AspNetCore.Identity;
using Services.Postgres.Services;

namespace Services.Postgres;

public sealed class PostgresModule : IServiceModule
{
    public ServiceManifest Manifest { get; } = ServiceManifest.Load(typeof(PostgresModule).Assembly);

    public void Configure(IServiceCollection services)
    {
        using var provider = services.BuildServiceProvider();
        var configuration = provider.GetService<IConfiguration>();
        services.AddSingleton<IDbProvider, PostgresDbProvider>();
        services.AddCompanyData(configuration?["Database:Provider"] ?? "sqlite", configuration?.GetConnectionString("Company") ?? "Data Source=company.db");
        services.AddIdentityCore<CompanyUser>().AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<CompanyDbContext>();
        services.AddSingleton<IServiceStartupTask, PostgresStartupTask>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/admin/model/entities", async (IModelExtensionService models, CancellationToken token) => Results.Ok(await models.GetEntities(token))).RequireAuthorization("OwnerOnly");
        endpoints.MapPost("/api/admin/model/fields", async (AddFieldRequest request, IModelExtensionService models, CancellationToken token) =>
        {
            try { return Results.Ok(await models.AddField(request, token)); }
            catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException) { return Results.BadRequest(new { error = exception.Message }); }
        }).RequireAuthorization("OwnerOnly");
        endpoints.MapPost("/api/admin/model/links", async (AddLinkRequest request, IModelExtensionService models, CancellationToken token) =>
        {
            try { return Results.Ok(await models.AddLink(request, token)); }
            catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException) { return Results.BadRequest(new { error = exception.Message }); }
        }).RequireAuthorization("OwnerOnly");
        endpoints.MapPut("/api/admin/model/users/{id:guid}/extensions/{fieldName}", async (Guid id, string fieldName, JsonElement value, CompanyDbContext database, IModelExtensionService models, CancellationToken token) =>
        {
            var userEntity = await database.EntityDefinitions.SingleAsync(entity => entity.Name == "User", token);
            var field = await database.FieldDefinitions.SingleOrDefaultAsync(item => item.EntityDefinitionId == userEntity.Id && item.Name == fieldName, token);
            var user = await database.Users.SingleOrDefaultAsync(item => item.Id == id, token);
            if (field is null || user is null) return Results.NotFound();
            try { await models.SetValue(user, field, value, token); return Results.NoContent(); }
            catch (InvalidOperationException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).RequireAuthorization("OwnerOnly");
        endpoints.MapGet("/api/admin/model/users/{id:guid}/related", async (Guid id, IRelatedRecordsService related, CancellationToken token) => Results.Ok(await related.GetFor("User", id, token))).RequireAuthorization();
    }
}

public sealed class PostgresStartupTask : IServiceStartupTask
{
    public async Task Run(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CompanyDbContext>>();
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await database.Database.EnsureCreatedAsync(cancellationToken);
        await DatabaseSchemaMigrator.ApplyModelMetadata(database);
        if (!await database.EntityDefinitions.AnyAsync(cancellationToken))
        {
            database.EntityDefinitions.Add(new EntityDefinition { Id = Guid.NewGuid(), Name = "User", IsBase = true });
            var entityTypes = scope.ServiceProvider.GetRequiredService<EntityTypeRegistry>();
            foreach (var type in entityTypes.Types.Where(type => type.Namespace?.StartsWith("Services.Companies.Contracts.Metadata", StringComparison.Ordinal) != true && type != typeof(ServiceState)))
                database.EntityDefinitions.Add(new EntityDefinition { Id = Guid.NewGuid(), Name = type.Name, IsBase = false });
            await database.SaveChangesAsync(cancellationToken);
        }
        if (scope.ServiceProvider.GetRequiredService<IServiceStateStore>() is ServiceStateStore states) await states.Load(cancellationToken);
    }
}
