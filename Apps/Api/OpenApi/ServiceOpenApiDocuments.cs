using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;

namespace Api.OpenApi;

/// <summary>
/// One OpenAPI document per registered service that ships controllers, discovered from the MVC
/// application parts rather than a hand-kept list — plugging a service in is what publishes its
/// document. The document is named after the service's singular folder (<c>Services/Faq</c> →
/// <c>faq</c>), which is also where <c>Scripts/generate-openapi.mjs</c> writes it.
/// </summary>
internal static class ServiceOpenApiDocuments
{
    private const string ServicePrefix = "CompanyHQ.";

    /// <summary>Call after every <c>Add{Service}()</c>, so every controller assembly is registered.</summary>
    public static void AddTo(IServiceCollection services, ApplicationPartManager parts)
    {
        var controllers = new ControllerFeature();
        parts.PopulateFeature(controllers);

        var assemblies = controllers.Controllers
            .Select(controller => controller.Assembly)
            .Distinct()
            .Where(assembly => assembly.GetName().Name!.StartsWith(ServicePrefix, StringComparison.Ordinal));

        foreach (var assembly in assemblies)
        {
            var assemblyName = assembly.GetName().Name!;
            services.AddOpenApi(DocumentName(assembly), options =>
            {
                options.ShouldInclude = description =>
                    description.ActionDescriptor is ControllerActionDescriptor controller
                    && string.Equals(
                        controller.ControllerTypeInfo.Assembly.GetName().Name,
                        assemblyName,
                        StringComparison.Ordinal);
            });
        }
    }

    // The template ships a singular folder under a plural assembly when the service owns an entity of
    // the same name (Services/User → CompanyHQ.Users), so the plural 's' is dropped.
    private static string DocumentName(Assembly assembly)
    {
        var service = assembly.GetName().Name![ServicePrefix.Length..].ToLowerInvariant();
        return service.EndsWith('s') ? service[..^1] : service;
    }
}
