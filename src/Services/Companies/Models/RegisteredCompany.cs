using Services.Companies.Contracts;

namespace Services.Companies.Models;

public sealed class RegisteredCompany : IEntityRecord
{
    public Guid Id { get; set; }
    public Dictionary<string, System.Text.Json.JsonElement> Extensions { get; set; } = [];
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public required string Host { get; set; }
    public required string AdminUrl { get; set; }
    public required string DbProvider { get; set; }
    public required string DatabaseName { get; set; }
    public required string DeploymentStatus { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
