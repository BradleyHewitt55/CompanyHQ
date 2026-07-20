// EF1001: the scaffolding pipeline (IScaffoldingModelFactory, NpgsqlDesignTimeServices)
// is internal EF surface. We accept the pin: it's the only way to diff the runtime-composed
// plugin model against the live database without design-time migration snapshots, which
// can't exist here because the model depends on which plugins a deployment loads.
#pragma warning disable EF1001

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql.EntityFrameworkCore.PostgreSQL.Design.Internal;

namespace CompanyHQ.Services.Postgress.Services;

// The database is the source of truth for its current schema (no stored snapshot
// state): restart-based lifecycle means every startup can afford to scaffold,
// diff, and apply. Shared by the startup migrator and company provisioning.
public sealed class SchemaSynchronizer(
    IConfiguration configuration,
    ILogger<SchemaSynchronizer> logger)
{
    public async Task SyncAsync(DbContext context, CancellationToken cancellationToken)
    {
        var creator = context.GetService<IRelationalDatabaseCreator>();

        if (!await creator.ExistsAsync(cancellationToken))
        {
            await creator.CreateAsync(cancellationToken);
            await creator.CreateTablesAsync(cancellationToken);
            logger.LogInformation("Created database and full schema from the plugin model.");
            return;
        }

        // Scaffolding a live database never reproduces the runtime model exactly
        // (explicit "public" schema, re-derived column facets), so the raw diff
        // contains operations that would change nothing. Filter those out first or
        // every startup logs phantom warnings — and AllowDestructiveMigrations
        // would execute pointless ALTERs.
        var operations = ComputeDifferences(context)
            .Where(operation => !IsNoise(operation))
            .ToList();
        if (operations.Count == 0)
        {
            return;
        }

        // Additive ops are what loading a new plugin produces; anything else can lose
        // data on a restart, so it stays behind an explicit opt-in.
        var allowDestructive = configuration.GetValue<bool>("Postgress:AllowDestructiveMigrations");
        var toApply = new List<MigrationOperation>();
        foreach (var operation in operations)
        {
            if (IsAdditive(operation) || allowDestructive)
            {
                toApply.Add(operation);
            }
            else
            {
                logger.LogWarning(
                    "Skipping non-additive schema change {Operation} on {Target}. " +
                    "Apply manually or set Postgress:AllowDestructiveMigrations.",
                    operation.GetType().Name,
                    Describe(operation));
            }
        }

        if (toApply.Count == 0)
        {
            return;
        }

        var model = context.GetService<IDesignTimeModel>().Model;
        var commands = context.GetService<IMigrationsSqlGenerator>().Generate(toApply, model);
        await context.GetService<IMigrationCommandExecutor>()
            .ExecuteNonQueryAsync(commands, context.GetService<IRelationalConnection>(), cancellationToken);

        logger.LogInformation("Applied {Count} schema change(s).", toApply.Count);
    }

    private static IReadOnlyList<MigrationOperation> ComputeDifferences(DbContext context)
    {
        // The differ needs both sides as EF models, so the live schema is reverse-engineered
        // through the same design-time services `dotnet ef dbcontext scaffold` uses.
        var design = new ServiceCollection();
        design.AddEntityFrameworkDesignTimeServices();
        design.AddDbContextDesignTimeServices(context);
        new NpgsqlDesignTimeServices().ConfigureDesignTimeServices(design);
        using var provider = design.BuildServiceProvider();

        var databaseModel = provider.GetRequiredService<IDatabaseModelFactory>()
            .Create(context.Database.GetConnectionString()!, new DatabaseModelFactoryOptions());
        var scaffolded = provider.GetRequiredService<IScaffoldingModelFactory>()
            .Create(databaseModel, new ModelReverseEngineerOptions());
        var source = provider.GetRequiredService<IModelRuntimeInitializer>()
            .Initialize(scaffolded, designTime: true, validationLogger: null);

        var target = context.GetService<IDesignTimeModel>().Model;

        return context.GetService<IMigrationsModelDiffer>()
            .GetDifferences(source.GetRelationalModel(), target.GetRelationalModel());
    }

    private static bool IsNoise(MigrationOperation operation) => operation switch
    {
        // Postgres's default schema: null and "public" are the same place.
        RenameTableOperation rename =>
            rename.Name == rename.NewName
            && (rename.Schema ?? "public") == (rename.NewSchema ?? "public"),
        AlterColumnOperation alter => ColumnsEquivalent(alter, alter.OldColumn),
        _ => false,
    };

    private static bool ColumnsEquivalent(ColumnOperation left, ColumnOperation right)
        => left.ColumnType == right.ColumnType
        && left.IsNullable == right.IsNullable
        && DefaultsEquivalent(left, right)
        && left.ComputedColumnSql == right.ComputedColumnSql
        && left.IsStored == right.IsStored
        && left.MaxLength == right.MaxLength
        && left.Precision == right.Precision
        && left.Scale == right.Scale
        && left.IsUnicode == right.IsUnicode
        && left.IsFixedLength == right.IsFixedLength
        && left.Collation == right.Collation
        // Identity/serial lives in this annotation, not a column facet; "None" and
        // absent mean the same thing.
        && Equals(
            left["Npgsql:ValueGenerationStrategy"]?.ToString() is null or "None"
                ? null : left["Npgsql:ValueGenerationStrategy"]!.ToString(),
            right["Npgsql:ValueGenerationStrategy"]?.ToString() is null or "None"
                ? null : right["Npgsql:ValueGenerationStrategy"]!.ToString());

    // A default that exists only in the database is our own backfill artifact:
    // adding a NOT NULL column emits DEFAULT to fill existing rows, and EF always
    // supplies values on insert, so the leftover default changes nothing. The cost
    // is that deliberately removing a model default is also never propagated —
    // acceptable, since a model with no default and a db default behave identically.
    private static bool DefaultsEquivalent(ColumnOperation left, ColumnOperation right)
        => (Equals(left.DefaultValue, right.DefaultValue) && left.DefaultValueSql == right.DefaultValueSql)
        || (left.DefaultValue is null && left.DefaultValueSql is null);

    private static string Describe(MigrationOperation operation) => operation switch
    {
        RenameTableOperation r => $"{r.Schema}.{r.Name} -> {r.NewSchema}.{r.NewName}",
        DropTableOperation d => $"{d.Schema ?? "public"}.{d.Name}",
        AlterColumnOperation a =>
            $"{a.Table}.{a.Name} ({a.OldColumn.ColumnType ?? a.OldColumn.ClrType.Name} -> {a.ColumnType ?? a.ClrType.Name}, " +
            $"nullable {a.OldColumn.IsNullable} -> {a.IsNullable})",
        DropColumnOperation d => $"{d.Table}.{d.Name}",
        ColumnOperation c => $"{c.Table}.{c.Name}",
        DropIndexOperation d => $"{d.Table}.{d.Name}",
        TableOperation t => t.Name,
        Microsoft.EntityFrameworkCore.Migrations.Operations.ITableMigrationOperation t => t.Table,
        _ => "?",
    };

    private static bool IsAdditive(MigrationOperation operation) => operation
        is EnsureSchemaOperation
        or CreateTableOperation
        or AddColumnOperation
        or CreateIndexOperation
        or AddPrimaryKeyOperation
        or AddForeignKeyOperation
        or AddUniqueConstraintOperation
        or CreateSequenceOperation;
}

#pragma warning restore EF1001
