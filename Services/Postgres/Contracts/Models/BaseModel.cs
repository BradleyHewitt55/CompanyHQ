namespace CompanyHQ.Services.Postgres.Contracts.Models;

/// <summary>
/// Root of every persisted CompanyHQ entity. Inheriting from this is what makes a type visible to
/// the Postgres service: the model registry discovers subclasses and maps them onto the shared
/// <c>PostgresDbContext</c>, and <see cref="IPostgresService{TModel}"/> only accepts these types.
/// </summary>
public abstract class BaseModel
{
    /// <summary>
    /// Assigned when the model is created. UUIDv7 is time-ordered, which keeps the primary-key index
    /// append-only instead of scattering inserts across the B-tree the way UUIDv4 does. The context
    /// still fills it on save if a caller explicitly resets it to <see cref="Guid.Empty"/>.
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
