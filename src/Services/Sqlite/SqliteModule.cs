using Services.Companies.Contracts.Data;
using Services.Companies.Contracts.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Routing;

namespace Services.Sqlite;

public sealed class SqliteModule : IServiceModule
{
    public ServiceManifest Manifest { get; } = ServiceManifest.Load(typeof(SqliteModule).Assembly);
    public void Configure(IServiceCollection services) => services.AddSingleton<IDbProvider, SqliteDbProvider>();
    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}

public sealed class SqliteDbProvider : IDbProvider
{
    public string Name => "sqlite";
    public void Configure(DbContextOptionsBuilder options, string connectionString) => options.UseSqlite(connectionString);
}
