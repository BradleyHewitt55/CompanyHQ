using Abstractions.Data;
using Microsoft.EntityFrameworkCore;

namespace CompanyHQ.Services.Postgress;

public sealed class PostgressDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    private readonly IEnumerable<IModelContributor> _contributors;

    public PostgressDbContext(
        DbContextOptions<PostgressDbContext> options,
        IEnumerable<IModelContributor> contributors)
        : base(options)
    {
        _contributors = contributors;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Inheriting BaseModel IS the table registration: any concrete subclass in a
        // loaded plugin becomes a table by convention, so plugins persist models
        // without writing any mapping code.
        var models = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .SelectMany(a =>
            {
                try
                {
                    return a.GetExportedTypes();
                }
                catch (Exception)
                {
                    // Assemblies with unloadable references can't contain usable models anyway.
                    return Type.EmptyTypes;
                }
            })
            .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(BaseModel)));

        foreach (var model in models)
        {
            modelBuilder.Entity(model);
        }

        // Contributors are the escape hatch for what conventions can't express:
        // indexes, relationships, column types, seed data.
        foreach (var contributor in _contributors)
        {
            contributor.Configure(modelBuilder);
        }
    }
}
