using System.Reflection;
using CompanyHQ.Services.Postgres.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace CompanyHQ.Services.Mcp.Services;

/// <summary>Discovers marked API actions and binds the MCP adapters their owning services ship.</summary>
public static class McpActionRegistry
{
    public static IReadOnlyDictionary<string, McpActionAttribute> Discover(ApplicationPartManager parts)
    {
        var feature = new ControllerFeature();
        parts.PopulateFeature(feature);
        var actions = feature.Controllers
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            .Select(method => method.GetCustomAttribute<McpActionAttribute>())
            .Where(attribute => attribute is not null)
            .Cast<McpActionAttribute>()
            .ToArray();
        if (actions.Any(action => !AccessPermissions.All.Contains(action.Permission, StringComparer.Ordinal)))
            throw new InvalidOperationException("An MCP action names an unknown permission.");
        if (actions.GroupBy(action => action.Name, StringComparer.Ordinal).Any(group => group.Count() != 1))
            throw new InvalidOperationException("MCP action names must be unique.");
        return actions.ToDictionary(action => action.Name, StringComparer.Ordinal);
    }

    /// <summary>
    /// Pairs each marked action with the one adapter of the same name. Adapters live in the service
    /// that owns the action, so they are found in the registered application-part assemblies and Mcp
    /// references none of those services.
    /// </summary>
    public static IReadOnlyList<McpServerTool> CreateTools(
        IReadOnlyDictionary<string, McpActionAttribute> actions,
        ApplicationPartManager parts)
    {
        var adapters = parts.ApplicationParts
            .OfType<AssemblyPart>()
            .Select(part => part.Assembly)
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Select(method => (Type: type, Method: method,
                    Attribute: method.GetCustomAttribute<McpServerToolAttribute>())))
            .Where(item => item.Attribute is not null)
            .ToArray();

        var tools = new List<McpServerTool>();
        foreach (var (name, action) in actions)
        {
            var matches = adapters.Where(item => item.Attribute!.Name == name).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"The marked API action '{name}' needs exactly one MCP adapter.");
            var adapter = matches[0];
            tools.Add(McpServerTool.Create(
                adapter.Method,
                context => ActivatorUtilities.CreateInstance(
                    context.Services ?? throw new InvalidOperationException("No MCP request services."), adapter.Type),
                new McpServerToolCreateOptions
                {
                    Name = name,
                    Description = action.Description
                }));
        }
        return tools;
    }
}
