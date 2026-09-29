using CompanyHQ.Services.Postgres.Data;

namespace CompanyHQ.Services.Postgres.Services;

/// <summary>
/// A unit of startup data that must exist before the host serves traffic. Implementations run after
/// migrations, in <see cref="Order"/>, and must be idempotent — they run on every boot.
/// </summary>
public interface IDataSeeder
{
    /// <summary>Lower runs first. Use it when one seeder depends on another's rows.</summary>
    int Order => 0;

    Task SeedAsync(PostgresDbContext context, CancellationToken cancellationToken);
}
