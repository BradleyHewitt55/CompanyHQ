using Microsoft.EntityFrameworkCore;

namespace Services.Companies.Contracts.Data;

public interface IDbProvider
{
    string Name { get; }
    void Configure(DbContextOptionsBuilder options, string connectionString);
}
