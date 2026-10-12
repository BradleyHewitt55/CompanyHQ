namespace CompanyHQ.Services.Persistence.Services;

/// <summary>
/// A unit of startup data that must exist before the host serves traffic. Implementations run once
/// the schema is current, in <see cref="Order"/>, and must be idempotent — they run on every boot.
/// </summary>
public interface IDataSeeder
{
    /// <summary>Lower runs first. Use it when one seeder depends on another's rows.</summary>
    int Order => 0;

    /// <summary>
    /// Every seeder is handed the same unit of work. Save what you stage before returning; whatever
    /// is still unsaved is discarded before the next seeder runs.
    /// </summary>
    Task SeedAsync(IPersistence persistence, CancellationToken cancellationToken);
}
