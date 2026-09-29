namespace CompanyHQ.Services.Postgres.Contracts;

/// <summary>Marks an HTTP action as an MCP operation with a stable name and required permission.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class McpActionAttribute(string name, string permission, string description) : Attribute
{
    public string Name { get; } = name;
    public string Permission { get; } = permission;
    public string Description { get; } = description;
}
