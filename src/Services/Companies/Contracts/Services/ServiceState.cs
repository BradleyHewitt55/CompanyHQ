using Services.Companies.Contracts;

namespace Services.Companies.Contracts.Services;

public sealed class ServiceState : IEntityRecord
{
    public Guid Id { get; set; }
    public Dictionary<string, System.Text.Json.JsonElement> Extensions { get; set; } = [];
    public required string ServiceId { get; set; }
    public bool Enabled { get; set; } = true;
    public long EnrollmentSequence { get; set; }
}
