using System.Globalization;
using System.Text.Json;
using Services.Companies.Contracts;
using Microsoft.EntityFrameworkCore;
using Services.Auth.Models;
using Services.Companies.Contracts.Metadata;

namespace Services.Postgres.Metadata;

public sealed class ModelExtensionService(CompanyDbContext database) : IModelExtensionService
{
    public async Task<FieldDefinition> AddField(AddFieldRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await FindEntity(request.Entity, cancellationToken);
        if (entity.IsBase && request.Entity.Equals("User", StringComparison.OrdinalIgnoreCase) && typeof(CompanyUser).GetProperties().Any(property => property.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Base field '{request.Name}' is sealed and cannot be altered.");
        if (await database.FieldDefinitions.AnyAsync(field => field.EntityDefinitionId == entity.Id && field.Name == request.Name, cancellationToken))
            throw new InvalidOperationException($"Field '{request.Name}' already exists on {request.Entity}.");
        var definition = new FieldDefinition { Id = Guid.NewGuid(), EntityDefinitionId = entity.Id, Name = request.Name, FieldType = request.Type };
        database.Add(definition); await database.SaveChangesAsync(cancellationToken); return definition;
    }

    public async Task<LinkDefinition> AddLink(AddLinkRequest request, CancellationToken cancellationToken = default)
    {
        var source = await FindEntity(request.Source, cancellationToken);
        var target = await FindEntity(request.Target, cancellationToken);
        if (source.Id == target.Id) throw new InvalidOperationException("Self-links are not allowed.");
        if (await database.LinkDefinitions.AnyAsync(link => link.SourceEntityDefinitionId == source.Id && link.TargetEntityDefinitionId == target.Id, cancellationToken))
            throw new InvalidOperationException("That link already exists.");
        var link = new LinkDefinition { Id = Guid.NewGuid(), SourceEntityDefinitionId = source.Id, TargetEntityDefinitionId = target.Id, Cardinality = request.Cardinality };
        database.Add(link); await database.SaveChangesAsync(cancellationToken); return link;
    }

    public async Task SetValue(IExtensibleEntity entity, FieldDefinition field, JsonElement value, CancellationToken cancellationToken = default)
    {
        Validate(field.FieldType, value);
        entity.Extensions[field.Name] = value.Clone();
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EntityDefinition>> GetEntities(CancellationToken cancellationToken = default) =>
        await database.EntityDefinitions.AsNoTracking().OrderBy(entity => entity.Name).ToListAsync(cancellationToken);

    private async Task<EntityDefinition> FindEntity(string name, CancellationToken cancellationToken)
    {
        var entity = await database.EntityDefinitions.SingleOrDefaultAsync(item => item.Name == name, cancellationToken);
        return entity ?? throw new KeyNotFoundException($"Unknown entity '{name}'.");
    }

    public static void Validate(ExtensionFieldType type, JsonElement value)
    {
        var valid = type switch
        {
            ExtensionFieldType.String => value.ValueKind == JsonValueKind.String,
            ExtensionFieldType.Number => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _),
            ExtensionFieldType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            ExtensionFieldType.Date => value.ValueKind == JsonValueKind.String && DateOnly.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            _ => false
        };
        if (!valid) throw new InvalidOperationException($"Value is not a valid {type}.");
    }
}
