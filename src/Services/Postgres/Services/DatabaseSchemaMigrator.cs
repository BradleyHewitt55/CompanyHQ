using System.Data;
using Microsoft.EntityFrameworkCore;
using Services.Companies.Contracts;
using Services.Companies.Contracts.Metadata;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Services.Postgres.Services;

public static class DatabaseSchemaMigrator
{
    public static async Task ApplyModelMetadata(CompanyDbContext database, CancellationToken cancellationToken = default)
    {
        var postgres = database.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
        var guid = postgres ? "uuid" : "TEXT";
        var json = postgres ? "jsonb" : "TEXT";
        var boolean = postgres ? "boolean" : "INTEGER";
        var emptyJson = postgres ? "'{}'::jsonb" : "'{}'";
        foreach (var sql in new[]
        {
            $"CREATE TABLE IF NOT EXISTS \"EntityDefinitions\" (\"Id\" {guid} NOT NULL PRIMARY KEY, \"Extensions\" {json} NOT NULL DEFAULT {emptyJson}, \"Name\" TEXT NOT NULL, \"IsBase\" {boolean} NOT NULL)",
            $"CREATE TABLE IF NOT EXISTS \"FieldDefinitions\" (\"Id\" {guid} NOT NULL PRIMARY KEY, \"Extensions\" {json} NOT NULL DEFAULT {emptyJson}, \"EntityDefinitionId\" {guid} NOT NULL, \"Name\" TEXT NOT NULL, \"FieldType\" INTEGER NOT NULL, \"IsBase\" {boolean} NOT NULL)",
            $"CREATE TABLE IF NOT EXISTS \"LinkDefinitions\" (\"Id\" {guid} NOT NULL PRIMARY KEY, \"Extensions\" {json} NOT NULL DEFAULT {emptyJson}, \"SourceEntityDefinitionId\" {guid} NOT NULL, \"TargetEntityDefinitionId\" {guid} NOT NULL, \"Cardinality\" INTEGER NOT NULL)",
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_EntityDefinitions_Name\" ON \"EntityDefinitions\" (\"Name\")",
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_FieldDefinitions_EntityDefinitionId_Name\" ON \"FieldDefinitions\" (\"EntityDefinitionId\", \"Name\")",
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_LinkDefinitions_SourceEntityDefinitionId_TargetEntityDefinitionId\" ON \"LinkDefinitions\" (\"SourceEntityDefinitionId\", \"TargetEntityDefinitionId\")"
        }) await Execute(database, sql, cancellationToken);

        foreach (var entityType in database.Model.GetEntityTypes().Where(entity => typeof(IExtensibleEntity).IsAssignableFrom(entity.ClrType)))
        {
            var table = entityType.GetTableName();
            if (table is not null && await HasTable(database, table, postgres, cancellationToken) && !await HasColumn(database, table, "Extensions", postgres, cancellationToken))
                await Execute(database, $"ALTER TABLE \"{table}\" ADD COLUMN \"Extensions\" {json} NOT NULL DEFAULT {emptyJson}", cancellationToken);
        }
        if (await HasTable(database, "ServiceStates", postgres, cancellationToken) && !await HasColumn(database, "ServiceStates", "EnrollmentSequence", postgres, cancellationToken))
            await Execute(database, "ALTER TABLE \"ServiceStates\" ADD COLUMN \"EnrollmentSequence\" bigint NOT NULL DEFAULT 0", cancellationToken);
    }

    private static async Task<bool> HasTable(CompanyDbContext database, string table, bool postgres, CancellationToken token) =>
        await Scalar(database, postgres ? "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = @name" : "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name", table, token) > 0;

    private static async Task<bool> HasColumn(CompanyDbContext database, string table, string column, bool postgres, CancellationToken token)
    {
        if (postgres) return await Scalar(database, $"SELECT COUNT(*) FROM information_schema.columns WHERE table_name = '{table.Replace("'", "''")}' AND column_name = @name", column, token) > 0;
        var connection = database.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(token);
        await using var command = connection.CreateCommand(); command.CommandText = $"PRAGMA table_info('{table}')";
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) if (reader.GetString(1).Equals(column, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static async Task<long> Scalar(CompanyDbContext database, string sql, string value, CancellationToken token)
    {
        var connection = database.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(token);
        await using var command = connection.CreateCommand(); command.CommandText = sql;
        var parameter = command.CreateParameter(); parameter.ParameterName = "@name"; parameter.Value = value; command.Parameters.Add(parameter);
        return Convert.ToInt64(await command.ExecuteScalarAsync(token));
    }

    private static async Task Execute(CompanyDbContext database, string sql, CancellationToken token)
    {
        var connection = database.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(token);
        await using var command = connection.CreateCommand(); command.CommandText = sql;
        await command.ExecuteNonQueryAsync(token);
    }
}
