using Admin.Components;
using Admin.Infrastructure;
using CompanyHQ.Services.Plugins;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddCompanyHqPlugins(builder.Configuration);

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

// Tenant first: plugin middleware (cookie validation included) reads the ambient
// company, so resolving it afterwards would authorize against an unset tenant.
app.UseMiddleware<CompanyResolutionMiddleware>();
app.UseCompanyHqPlugins();

app.UseAntiforgery();
app.MapStaticAssets();
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(CompanyHqPlugins.RouteAssemblies);
app.Run();

public partial class Program;
