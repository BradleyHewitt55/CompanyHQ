using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore;
using CompanyHQ.Postgres.Contracts.Models;

namespace CompanyHQ.Postgres.Data;

/// <summary>
/// The single context over every <see cref="BaseModel"/> in the application. Services do not define
/// their own context; they define models and go through the Persistence ports this provider fills.
/// </summary>
public class PostgresDbContext(
    DbContextOptions<PostgresDbContext> options,
    PostgresModelRegistry registry) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        registry.Apply(modelBuilder);

        foreach (var entity in modelBuilder.Model.GetEntityTypes()
            .Where(entity => typeof(BaseModel).IsAssignableFrom(entity.ClrType)))
        {
            // Ids are assigned when models are created, with SaveChanges as a fallback, so telling
            // EF the database generates them would make it discard the value we set.
            entity.FindProperty(nameof(BaseModel.Id))!.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
        }
    }

    public override int SaveChanges()
    {
        StampEntries();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampEntries();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Fills in missing ids and timestamps for every tracked model, so callers cannot forget and so
    /// the values are identical whether the write came through the repository or raw EF.
    /// </summary>
    private void StampEntries()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseModel>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.Id == Guid.Empty)
                    {
                        entry.Entity.Id = Guid.CreateVersion7(now);
                    }

                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;

                case EntityState.Modified:
                    entry.Property(model => model.CreatedAt).IsModified = false;
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }
}
