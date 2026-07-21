using Admin.Components;
using Abstractions.Plugins;
using Abstractions.Services;
using CompanyHQ.Infrastructure.PluginDiscovery;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddPluginDiscovery(builder.Configuration);
builder.Services.AddScoped<IServiceCatalog, MenuItemService>();

var app = builder.Build();

// Kestrel only ever sits behind the reverse proxy (docs/Operations/Hosting.md), so
// the original scheme/host/client arrive as X-Forwarded-* headers; without honoring
// them every redirect and cookie would downgrade to http, and the login rate limiter
// would see the proxy's address instead of the client's and throttle everyone together.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
                       | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost
                       | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor,
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Plugin middleware reads the company pre-bound into every scoped ICompanyContext.
app.UseDiscoveredPlugins();

app.UseAntiforgery();
app.MapStaticAssets();
app.MapControllers();

var pluginCatalog = app.Services.GetRequiredService<IPluginCatalog>();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(pluginCatalog.RouteAssemblies.ToArray());
app.Run();

public partial class Program;
