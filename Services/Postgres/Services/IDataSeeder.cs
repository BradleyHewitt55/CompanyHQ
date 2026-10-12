using CompanyHQ.Services.Postgres.Data;

namespace CompanyHQ.Services.Postgres.Services;

/// <summary>
/// A unit of startup data that must exist before the host serves traffic. Implementations run after
/// migrations, in <see cref="Order"/>, and must be idempotent — they run on every boot.
/// </summary>
/// <remarks>
/// This is the provider's own form, handed the EF Core context. A seeder that only needs ordinary
/// reads and writes implements the provider-neutral <c>IDataSeeder</c> in Persistence instead; both
/// kinds run in one sequence, ordered together.
/// </remarks>
public interface IDataSeeder
{
    /// <summary>Lower runs first. Use it when one seeder depends on another's rows.</summary>
    int Order => 0;

    Task SeedAsync(PostgresDbContext context, CancellationToken cancellationToken);
}
