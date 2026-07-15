using System.Text.Json;

namespace Services.Companies.Contracts.Metadata;

public interface IModelExtensionService
{
    Task<FieldDefinition> AddField(AddFieldRequest request, CancellationToken cancellationToken = default);
    Task<LinkDefinition> AddLink(AddLinkRequest request, CancellationToken cancellationToken = default);
    Task SetValue(IExtensibleEntity entity, FieldDefinition field, JsonElement value, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EntityDefinition>> GetEntities(CancellationToken cancellationToken = default);
}

public sealed record RelatedRecord(string Entity, Guid Id, string Label);

public interface IRelatedRecordsService
{
    Task<IReadOnlyList<RelatedRecord>> GetFor(string sourceEntity, Guid sourceId, CancellationToken cancellationToken = default);
}
