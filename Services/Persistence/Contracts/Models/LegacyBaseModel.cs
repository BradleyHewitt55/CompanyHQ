namespace CompanyHQ.Postgres.Contracts.Models;

/// <summary>Provider-neutral persistence identity. The historic namespace is retained for consumers.</summary>
public abstract class BaseModel
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
