using Services.Companies.Contracts;
using Services.Companies.Contracts.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Services.Auth.Models;
using Services.Companies.Contracts.Metadata;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Services.Postgres;

public sealed class CompanyDbContext(DbContextOptions<CompanyDbContext> options, EntityTypeRegistry entityTypes) : IdentityDbContext<CompanyUser, IdentityRole<Guid>, Guid>(options)
{
    internal string ModelKey => string.Join('|', entityTypes.Types.Select(type => type.AssemblyQualifiedName).Order(StringComparer.Ordinal));
    public DbSet<ServiceState> ServiceStates => Set<ServiceState>();
    public DbSet<EntityDefinition> EntityDefinitions => Set<EntityDefinition>();
    public DbSet<FieldDefinition> FieldDefinitions => Set<FieldDefinition>();
    public DbSet<LinkDefinition> LinkDefinitions => Set<LinkDefinition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        foreach (var type in entityTypes.Types) modelBuilder.Entity(type);
        foreach (var assembly in entityTypes.Assemblies) modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        modelBuilder.Entity<EntityDefinition>().HasIndex(entity => entity.Name).IsUnique();
        modelBuilder.Entity<FieldDefinition>().HasIndex(field => new { field.EntityDefinitionId, field.Name }).IsUnique();
        modelBuilder.Entity<LinkDefinition>().HasIndex(link => new { link.SourceEntityDefinitionId, link.TargetEntityDefinitionId }).IsUnique();
        modelBuilder.Entity<ServiceState>().HasIndex(state => state.ServiceId).IsUnique();
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(entity => typeof(IExtensibleEntity).IsAssignableFrom(entity.ClrType)))
        {
            var property = modelBuilder.Entity(entityType.ClrType).Property<Dictionary<string, JsonElement>>(nameof(IExtensibleEntity.Extensions));
            property.HasConversion(new ValueConverter<Dictionary<string, JsonElement>, string>(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                value => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(value, (JsonSerializerOptions?)null) ?? new()));
            property.Metadata.SetValueComparer(new ValueComparer<Dictionary<string, JsonElement>>(
                (left, right) => JsonSerializer.Serialize(left, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(right, (JsonSerializerOptions?)null),
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null).GetHashCode(),
                value => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(value, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!));
            if (Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true) property.HasColumnType("jsonb");
        }
    }
}
