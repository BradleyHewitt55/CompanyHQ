using Services.Companies.Contracts;

namespace Services.Companies.Contracts.Metadata;

public enum ExtensionFieldType { String, Number, Boolean, Date }
public enum LinkCardinality { OneToMany, ManyToMany }

public abstract class MetadataModel : IEntityRecord
{
    public Guid Id { get; set; }
    public Dictionary<string, System.Text.Json.JsonElement> Extensions { get; set; } = [];
}

public sealed class EntityDefinition : MetadataModel
{
    public required string Name { get; set; }
    public bool IsBase { get; set; }
}

public sealed class FieldDefinition : MetadataModel
{
    public Guid EntityDefinitionId { get; set; }
    public required string Name { get; set; }
    public ExtensionFieldType FieldType { get; set; }
    public bool IsBase { get; set; }
}

public sealed class LinkDefinition : MetadataModel
{
    public Guid SourceEntityDefinitionId { get; set; }
    public Guid TargetEntityDefinitionId { get; set; }
    public LinkCardinality Cardinality { get; set; }
}

public sealed record AddFieldRequest(string Entity, string Name, ExtensionFieldType Type);
public sealed record AddLinkRequest(string Source, string Target, LinkCardinality Cardinality);
