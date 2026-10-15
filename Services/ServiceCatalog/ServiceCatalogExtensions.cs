using System.Reflection;
using System.Runtime.ExceptionServices;
using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.ServiceCatalog;

public static class ServiceCatalogExtensions
{
    /// <summary>Adds metadata for company-owned configuration flags without storing their values.</summary>
    public static IServiceCollection AddServiceConfigurationKeys(this IServiceCollection services, string key,
        params ServiceConfigurationKey[] keys)
    {
        var entry = services.Single(item => item.ServiceType == typeof(ServiceRegistration)
            && ((ServiceRegistration)item.ImplementationInstance!).Descriptor.Key == key);
        var registration = (ServiceRegistration)entry.ImplementationInstance!;
        var combined = (registration.Descriptor.ConfigurationKeys ?? []).Concat(keys).ToArray();
        if (combined.GroupBy(item => item.Key, StringComparer.Ordinal).Any(group => group.Count() != 1))
            throw new ArgumentException("Configuration metadata keys must be distinct.");
        services.Remove(entry);
        services.AddSingleton(registration with { Descriptor = registration.Descriptor with { ConfigurationKeys = combined } });
        return services;
    }

    /// <summary>Company hosts own page destinations. Call after the feature descriptor and before AddServiceCatalog.</summary>
    public static IServiceCollection ConfigureServiceNavigation(this IServiceCollection services, string key,
        string? destination, string? readPermission, params string[] additionalReadPermissions)
    {
        if (destination is not null && (!destination.StartsWith('/') || destination.StartsWith("//", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(readPermission)))
            throw new ArgumentException("A navigation destination must be an application path with a named permission.");
        var entry = services.Single(item => item.ServiceType == typeof(ServiceRegistration)
            && ((ServiceRegistration)item.ImplementationInstance!).Descriptor.Key == key);
        var registration = (ServiceRegistration)entry.ImplementationInstance!;
        services.Remove(entry);
        services.AddSingleton(registration with { Descriptor = registration.Descriptor with
            { MenuPath = destination, ReadPermission = readPermission, AdditionalReadPermissions = additionalReadPermissions } });
        return services;
    }

    public static IServiceCollection AddServiceDescriptor(this IServiceCollection services, ServiceRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        var descriptor = registration.Descriptor;
        if (string.IsNullOrWhiteSpace(descriptor.Key) || descriptor.Key.Any(character => !char.IsAsciiLetterLower(character) && !char.IsAsciiDigit(character)))
            throw new ArgumentException("Use a canonical lowercase package suffix for a service key.");
        if (registration.LoadedProvider is not null)
        {
            if (!(descriptor.Providers ?? []).Any(provider => provider.Key == registration.LoadedProvider))
                throw new ArgumentException("The loaded provider must be declared in the descriptor.");
            registration = registration with { Descriptor = descriptor with { DefaultProvider = registration.LoadedProvider } };
        }
        services.AddSingleton(registration);
        return services;
    }

    /// <summary>Call once, after feature and bridge registration. Only allowlisted features are admitted.</summary>
    public static IServiceCollection AddServiceCatalog(this IServiceCollection services, params string[] allowedServices)
    {
        if (!services.Any(item => item.ServiceType == typeof(IServiceSelectionReader)))
            throw new InvalidOperationException("The catalog requires an always-enabled committed selection reader.");
        var registrations = services.Where(item => item.ServiceType == typeof(ServiceRegistration))
            .Select(item => (ServiceRegistration)item.ImplementationInstance!).ToArray();
        if (registrations.GroupBy(item => item.Descriptor.Key).Any(group => group.Count() != 1))
            throw new InvalidOperationException("Each service must register exactly one descriptor.");
        var allowlist = allowedServices.ToHashSet(StringComparer.Ordinal);
        if (allowlist.Count != allowedServices.Length || allowlist.Any(key => registrations.All(item => item.Descriptor.Key != key)))
            throw new InvalidOperationException("The catalog allowlist must name distinct registered services.");
        var registry = new ServiceCatalogRegistry(registrations.Where(item => allowlist.Contains(item.Descriptor.Key)));
        foreach (var item in registry.Services.Values)
        {
            if (item.Descriptor.MenuPath is not null && item.Descriptor.ReadPermission is null)
                throw new InvalidOperationException($"Service '{item.Descriptor.Key}' must name a navigation read permission.");
            var visited = new HashSet<string>(StringComparer.Ordinal);
            void Visit(string key)
            {
                if (!registry.Services.TryGetValue(key, out var dependency)) throw new InvalidOperationException($"Required catalog dependency '{key}' is not allowlisted.");
                if (!visited.Add(key)) throw new InvalidOperationException("Catalog dependencies must be acyclic.");
                foreach (var child in dependency.Descriptor.Dependencies ?? []) Visit(child);
                visited.Remove(key);
            }
            Visit(item.Descriptor.Key);
        }
        services.AddSingleton(registry);
        services.AddSingleton<IServiceAvailability, ServiceAvailability>();

        // Explicit opt-in decoration leaves published strict registrations and constructors unchanged.
        // Concrete constructor registrations remain visible to DI validation: missing ports still fail at build.
        foreach (var original in services.ToArray())
        {
            if (!original.ServiceType.IsInterface || original.IsKeyedService) continue;
            var owner = registrations.FirstOrDefault(item => original.ServiceType.Assembly == item.Assembly
                || original.ServiceType.Assembly.GetName().Name == item.Assembly.GetName().Name + ".Contracts");
            if (owner is null || owner.Descriptor.Key is "company" or "auth" or "users" or "rss") continue;
            var targetKey = new object();
            if (original.ImplementationType is { } implementation)
                services.Add(Microsoft.Extensions.DependencyInjection.ServiceDescriptor.DescribeKeyed(implementation, targetKey, implementation, original.Lifetime));
            services.Remove(original);
            services.Add(new Microsoft.Extensions.DependencyInjection.ServiceDescriptor(original.ServiceType, provider =>
            {
                var target = original.ImplementationInstance ?? original.ImplementationFactory?.Invoke(provider)
                    ?? provider.GetRequiredKeyedService(original.ImplementationType!, targetKey);
                var proxy = (ServiceAdmissionProxy)DispatchProxy.Create(original.ServiceType, typeof(ServiceAdmissionProxy));
                proxy.Initialize(target, provider.GetRequiredService<IServiceAvailability>(), owner.Descriptor.Key,
                    owner.Descriptor.Key is "media" or "geo" || original.ServiceType.Name is "IEmailProvider" or "IPushSender");
                return proxy;
            }, original.Lifetime));
        }
        return services;
    }

    /// <summary>Place after routing and before authentication, authorization and endpoint execution.</summary>
    public static IApplicationBuilder UseServiceCatalog(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        try
        {
            var endpoint = context.GetEndpoint();
            var owner = endpoint?.Metadata.GetMetadata<ServiceOwnerAttribute>()?.Key;
            if (owner is null && endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>() is { } action)
            {
                owner = context.RequestServices.GetRequiredService<ServiceCatalogRegistry>().Services.Values
                    .FirstOrDefault(item => item.Assembly == action.ControllerTypeInfo.Assembly)?.Descriptor.Key;
                // Registered feature assemblies excluded by the allowlist must also be absent over HTTP.
                owner ??= context.RequestServices.GetServices<ServiceRegistration>()
                    .FirstOrDefault(item => item.Assembly == action.ControllerTypeInfo.Assembly)?.Descriptor.Key;
            }
            if (owner is not null)
                await context.RequestServices.GetRequiredService<IServiceAvailability>().RequireAsync(owner, cancellationToken: context.RequestAborted);
            await next(context);
        }
        catch (ServiceUnavailableException exception) when (!context.Response.HasStarted)
        {
            context.Response.Clear();
            context.Response.StatusCode = exception.Status == ServiceStatus.Disabled ? 404 : 503;
        }
        catch (ServiceDependencyUnavailableException) when (!context.Response.HasStarted)
        {
            context.Response.Clear();
            context.Response.StatusCode = 503;
        }
    });
}

/// <summary>Admission checks run for every interface operation, including calls from host bridges.</summary>
public class ServiceAdmissionProxy : DispatchProxy
{
    private object target = null!;
    private IServiceAvailability availability = null!;
    private string key = null!;
    private bool ready;
    internal void Initialize(object target, IServiceAvailability availability, string key, bool ready) =>
        (this.target, this.availability, this.key, this.ready) = (target, availability, key, ready);

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        // Port identity/readiness properties are needed while constructing other services.
        // They execute no domain/provider operation and must not poison optional integrations.
        if (method!.IsSpecialName && method.Name.StartsWith("get_", StringComparison.Ordinal)) return Call(method, args);
        var cancellation = args?.OfType<CancellationToken>().LastOrDefault() ?? default;
        if (method!.ReturnType == typeof(Task)) return InvokeTask(method, args, cancellation);
        if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
            return typeof(ServiceAdmissionProxy).GetMethod(nameof(InvokeTaskOf), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(method.ReturnType.GenericTypeArguments).Invoke(this, [method, args, cancellation]);
        availability.RequireAsync(key, ready, cancellation).GetAwaiter().GetResult();
        return Call(method, args);
    }

    private object? Call(MethodInfo method, object?[]? args)
    {
        try { return method.Invoke(target, args); }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
    }
    private async Task InvokeTask(MethodInfo method, object?[]? args, CancellationToken cancellation)
    {
        await availability.RequireAsync(key, ready, cancellation);
        await (Task)Call(method, args)!;
    }
    private async Task<T> InvokeTaskOf<T>(MethodInfo method, object?[]? args, CancellationToken cancellation)
    {
        await availability.RequireAsync(key, ready, cancellation);
        return await (Task<T>)Call(method, args)!;
    }
}
