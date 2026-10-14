using System.Collections.Immutable;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyModel;
using CompanyHQ.Postgres.Contracts.Models;

namespace CompanyHQ.Postgres.Data;

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
    /// declare a model — anything referencing the Persistence assembly that owns
    /// <see cref="BaseModel"/>, or this provider, which older services reference for it — for
    /// concrete <see cref="BaseModel"/> subclasses. Ordered by full name so the model, and therefore
    /// migration output, is stable across runs.
    /// </summary>
    public static PostgresModelRegistry Discover(Assembly hostAssembly)
    {
        ArgumentNullException.ThrowIfNull(hostAssembly);

        // A service sees BaseModel through Persistence, or through this provider if it was written
        // before the two were split. Missing either would silently drop its tables from the model.
        string?[] owners = [typeof(BaseModel).Assembly.GetName().Name, typeof(PostgresModelRegistry).Assembly.GetName().Name];
        var dependencies = DependencyContext.Load(hostAssembly)
            ?? throw new InvalidOperationException(
                $"{hostAssembly.GetName().Name} has no deps.json, so its model assemblies cannot be discovered.");

        // A company host consumes the same services as packages, so both library kinds are scanned;
        // a package's assembly need not share its id, hence the names come from the library itself.
        var modelTypes = dependencies.RuntimeLibraries
            .Where(library => library.Type is "project" or "package"
                && (owners.Contains(library.Name)
                    || library.Dependencies.Any(dependency => owners.Contains(dependency.Name))))
            .SelectMany(library => library.GetDefaultAssemblyNames(dependencies))
            .Select(Assembly.Load)
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
