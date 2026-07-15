using Services.Postgres.Providers;
using Services.Companies.Contracts.Services;
using Services.Companies.Contracts.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Services.Companies.Contracts.Metadata;
using Services.Postgres.Metadata;
using Services.Companies.Models;
using Services.Companies.Data;

namespace Services.Postgres;

public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddCompanyData(this IServiceCollection services, IDbProvider provider, string connectionString, Action<EntityTypeRegistry>? configureTypes = null)
    {
        services.AddSingleton(provider);
        return services.AddCompanyData(provider.Name, connectionString, configureTypes);
    }

    public static IServiceCollection AddCompanyData(this IServiceCollection services, string providerName, string connectionString, Action<EntityTypeRegistry>? configureTypes = null)
    {
        var types = new EntityTypeRegistry();
        types.Add(typeof(EntityDefinition));
        types.Add(typeof(FieldDefinition));
        types.Add(typeof(LinkDefinition));
        types.Add(typeof(CompanyProfile));
        configureTypes?.Invoke(types);
        services.AddSingleton(types);
        services.AddPooledDbContextFactory<CompanyDbContext>((serviceProvider, options) =>
        {
            var provider = serviceProvider.GetServices<IDbProvider>().SingleOrDefault(candidate => candidate.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Database provider '{providerName}' is not registered.");
            provider.Configure(options, connectionString);
            options.ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory, CompanyModelCacheKeyFactory>();
        });
        services.AddScoped(provider => provider.GetRequiredService<IDbContextFactory<CompanyDbContext>>().CreateDbContext());
        services.AddScoped(typeof(IEntityService<>), typeof(PostgresService<>));
        services.AddSingleton<IServiceStateStore, ServiceStateStore>();
        services.AddScoped<IModelExtensionService, ModelExtensionService>();
        services.AddScoped<IRelatedRecordsService, RelatedRecordsService>();
        services.AddScoped<ICompanyProfileStore, CompanyProfileStore>();
        return services;
    }
}
