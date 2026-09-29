using System.Collections.Immutable;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyModel;
using CompanyHQ.Services.Postgres.Contracts.Models;

namespace CompanyHQ.Services.Postgres.Data;

/// <summary>
/// The set of <see cref="BaseModel"/> types the shared <see cref="PostgresDbContext"/> maps.
/// </summary>
/// <remarks>
/// Discovery walks the host's dependency graph rather than a hand-kept list, so a service that adds
/// its first model is mapped by being referenced — nothing in the host or here changes. The running
/// host and the <c>dotnet ef</c> design-time factory both start the walk from the host assembly, so
/// the generated migration and the live schema cannot disagree.
/// </remarks>
public sealed class PostgresModelRegistry
{
    private PostgresModelRegistry(ImmutableArray<Type> modelTypes) => ModelTypes = modelTypes;

    public ImmutableArray<Type> ModelTypes { get; }

    /// <summary>
    /// Scans every project library in <paramref name="hostAssembly"/>'s dependency graph that can
    /// declare a model — this assembly and anything referencing it — for concrete
    /// <see cref="BaseModel"/> subclasses. Ordered by full name so the model, and therefore migration
    /// output, is stable across runs.
    /// </summary>
    public static PostgresModelRegistry Discover(Assembly hostAssembly)
    {
        ArgumentNullException.ThrowIfNull(hostAssembly);

        var postgres = typeof(BaseModel).Assembly.GetName().Name;
        var dependencies = DependencyContext.Load(hostAssembly)
            ?? throw new InvalidOperationException(
                $"{hostAssembly.GetName().Name} has no deps.json, so its model assemblies cannot be discovered.");

        var modelTypes = dependencies.RuntimeLibraries
            .Where(library => library.Type == "project"
                && (library.Name == postgres
                    || library.Dependencies.Any(dependency => dependency.Name == postgres)))
            .Select(library => Assembly.Load(new AssemblyName(library.Name)))
            .Append(hostAssembly)
            .Distinct()
            .SelectMany(GetLoadableTypes)
            .Where(type => type is { IsAbstract: false, IsGenericTypeDefinition: false }
                && typeof(BaseModel).IsAssignableFrom(type))
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToImmutableArray();

        return new PostgresModelRegistry(modelTypes);
    }

    /// <summary>Maps every discovered model, then applies any configurations in the same assemblies.</summary>
    internal void Apply(ModelBuilder modelBuilder)
    {
        foreach (var modelType in ModelTypes)
        {
            modelBuilder.Entity(modelType);
        }

        foreach (var assembly in ModelTypes.Select(type => type.Assembly).Distinct())
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }
    }

    // A model assembly can reference something optional that is absent at design time; map what
    // loaded instead of failing the whole scan.
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }
}
