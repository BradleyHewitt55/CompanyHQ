using Services.Companies.Contracts;

namespace Services.Companies.Models;

public sealed class CompanyProfile : IEntityRecord
{
    public Guid Id { get; set; }
    public Dictionary<string, System.Text.Json.JsonElement> Extensions { get; set; } = [];
    public required string Name { get; set; }
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}
