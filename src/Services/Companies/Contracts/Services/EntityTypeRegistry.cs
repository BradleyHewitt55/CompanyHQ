using Services.Companies.Contracts;
using System.Reflection;

namespace Services.Companies.Contracts.Services;

public sealed class EntityTypeRegistry
{
    private readonly HashSet<Type> types = [typeof(ServiceState)];
    private readonly HashSet<Assembly> assemblies = [];
    public IReadOnlyCollection<Type> Types => types;
    public IReadOnlyCollection<Assembly> Assemblies => assemblies;

    public void Add(Type type)
    {
        if (!typeof(IExtensibleEntity).IsAssignableFrom(type) || type.IsAbstract)
            throw new ArgumentException($"{type.FullName} must be a concrete {nameof(IExtensibleEntity)} type.", nameof(type));
        types.Add(type);
    }

    public void AddFromAssembly(Assembly assembly)
    {
        assemblies.Add(assembly);
        foreach (var type in assembly.GetTypes().Where(type => !type.IsAbstract && typeof(IExtensibleEntity).IsAssignableFrom(type))) Add(type);
    }
}
