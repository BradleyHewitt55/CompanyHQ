using CompanyHQ.ServiceCatalog;
using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Widget.Extensions;

public static class WidgetCatalogExtensions
{
    /// <summary>Registers public metadata. The host explicitly allowlists this service in AddServiceCatalog.</summary>
    public static IServiceCollection AddWidgetServiceDescriptor(this IServiceCollection services,
        string? loadedProvider = null, Func<IServiceProvider, bool>? isConfigured = null,
        Func<IServiceProvider, string, CancellationToken, Task<bool>>? isCapabilityConfigured = null)
    {
        var hasConversations = services.Any(item => item.ServiceType == typeof(CompanyHQ.Widget.Contracts.Ports.IWidgetConversations));
        return
        services.AddServiceDescriptor(new(new(
            "widget", "Widget", "A chat and bug-report button for your website.",
            "Adds an embeddable widget where visitors start a conversation (answered in Inbox) or report a problem (sent to GitHub). Each part works when its service is on and configured.",
            Core: false, Dependencies: [],
            Providers: null,
            DefaultProvider: null, MenuPath: null, Capabilities: [new("conversations", "Visitor conversations through Inbox.", ["inbox"]), new("issues", "Issue reporting through GitHub.", ["github"])],
            ConfigurationKeys: [
                new("Widget:Sources", "Allowed company surface labels. Arrays use indexed environment keys.", Required: true)
            ]),
            typeof(WidgetCatalogExtensions).Assembly, loadedProvider,
            isConfigured, isCapabilityConfigured ?? ((_, capability, _) => Task.FromResult(capability == "conversations" && hasConversations))));
    }
}
