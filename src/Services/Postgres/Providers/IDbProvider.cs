using Microsoft.EntityFrameworkCore;
using Services.Companies.Contracts.Data;

namespace Services.Postgres.Providers;

public sealed class PostgresDbProvider : IDbProvider
{
    public string Name => "postgres";
    public void Configure(DbContextOptionsBuilder options, string connectionString) => options.UseNpgsql(connectionString);
}
