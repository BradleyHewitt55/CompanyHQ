using Services.Companies.Contracts;
using Services.Companies.Contracts.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.HttpOverrides;
using Web.Admin;
using Web.Admin.Components;
using Web.Admin.Services;
using Web.Components;
using Web.Components.Layout;
using Web.Modules;

var workingDirectory = Directory.GetCurrentDirectory();
var projectContentRoot = Directory.Exists(Path.Combine(workingDirectory, "wwwroot"))
    ? workingDirectory
    : Directory.Exists(Path.Combine(workingDirectory, "src", "Web", "wwwroot"))
        ? Path.Combine(workingDirectory, "src", "Web")
        : AppContext.BaseDirectory;
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = projectContentRoot });
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.Configure<ForwardedHeadersOptions>(options => options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
builder.Services.AddHealthChecks();

var registry = new ComponentRegistry(typeof(NotFoundMarker));
registry.RegisterDefaultAdminComponents();
var layouts = new LayoutRegistry();
layouts.Register("MainLayout", typeof(MainLayout));
layouts.Register("AdminLayout", typeof(AdminLayout));
layouts.Register("CompanyLayout", typeof(CompanyLayout));
builder.Services.AddSingleton(registry);
builder.Services.AddSingleton(layouts);
builder.Services.AddCompanyHQAdmin();
builder.Services.AddSingleton<IServiceStateStore, InMemoryServiceStateStore>();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["DataProtection:Path"] ?? Path.Combine(AppContext.BaseDirectory, ".data-protection")))
    .SetApplicationName(builder.Configuration["Company:Id"] ?? "companyhq-local");

using var bootstrapLoggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
var repositoryModules = FindRepositoryModules(workingDirectory) ?? FindRepositoryModules(AppContext.BaseDirectory) ?? string.Empty;
var bundledModules = Directory.Exists(repositoryModules) ? repositoryModules
    : Directory.Exists(Path.Combine(projectContentRoot, "bin", "Debug", "net10.0", "modules"))
        ? Path.Combine(projectContentRoot, "bin", "Debug", "net10.0", "modules")
        : Path.Combine(AppContext.BaseDirectory, "modules");
var configuredModules = builder.Configuration["ModulesPath"];
var modulesPath = string.IsNullOrWhiteSpace(configuredModules) || Path.GetFullPath(configuredModules).Equals(Path.GetFullPath(bundledModules), StringComparison.OrdinalIgnoreCase)
    ? bundledModules
    : bundledModules + Path.PathSeparator + configuredModules;
var moduleCatalog = ModuleCatalog.Load(modulesPath, builder.Services, registry, layouts, bootstrapLoggerFactory.CreateLogger("ModuleLoader"));
AdminOverrideLoader.Load(builder.Configuration["AdminOverridesPath"] ?? "/app/overrides/Web.Admin.Overrides.dll", registry, layouts, bootstrapLoggerFactory.CreateLogger("AdminOverrides"));
builder.Services.AddSingleton(moduleCatalog);
builder.Services.AddSingleton<IRuntimeServiceLoader, RuntimeServiceLoader>();
builder.Services.AddHttpClient();
builder.Services.RemoveAll<IServiceCatalog>();
builder.Services.AddSingleton<IServiceCatalog, ModuleServiceCatalog>();
builder.Services.RemoveAll<IAdminNavigationCatalog>();
builder.Services.AddSingleton<IAdminNavigationCatalog, ModuleNavigationCatalog>();

var app = builder.Build();
foreach (var startup in app.Services.GetServices<IServiceStartupTask>()) await startup.Run(app.Services);
var stateStore = app.Services.GetRequiredService<IServiceStateStore>();
moduleCatalog.StateStore = stateStore;
foreach (var module in moduleCatalog.Modules.Where(module => !module.Manifest.Hidden && !stateStore.Contains(module.Manifest.Id)))
    await stateStore.Enroll(module.Manifest.Id, true);

if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Error", createScopeForErrors: true); app.UseHsts(); }
app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseStatusCodePages();
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.XFrameOptions = "DENY";
        context.Response.Headers.ContentSecurityPolicy = "frame-ancestors 'none'";
        if (!app.Environment.IsDevelopment()) context.Response.Headers.StrictTransportSecurity = "max-age=31536000; includeSubDomains";
        return Task.CompletedTask;
    });
    await next();
});
app.Use(async (context, next) =>
{
    var serviceId = moduleCatalog.MatchService(context.Request.Path);
    if (serviceId is not null && !moduleCatalog.IsHidden(serviceId) && !moduleCatalog.IsEffectivelyEnabled(serviceId)) { context.Response.StatusCode = 404; return; }
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets().AllowAnonymous();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapGet("/companies", () => Results.Redirect("/admin/companies", permanent: true)).AllowAnonymous();
var componentAssemblies = moduleCatalog.Modules.Select(module => module.GetType().Assembly).Distinct().ToArray();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode().AddAdditionalAssemblies(componentAssemblies);
moduleCatalog.MapEndpoints(app);
app.MapGet("/api/services/{serviceId}", (string serviceId) =>
    moduleCatalog.Find(serviceId) is { } && !moduleCatalog.IsHidden(serviceId) && moduleCatalog.IsEffectivelyEnabled(serviceId)
        ? Results.Ok(new { service = serviceId })
        : Results.NotFound()).RequireAuthorization();
app.MapPut("/api/admin/services/{id}/enabled", async (HttpContext context, string id, bool enabled, IServiceStateStore states) =>
{
    if (context.Features.Get<Microsoft.AspNetCore.Antiforgery.IAntiforgeryValidationFeature>()?.IsValid == false) return Results.BadRequest();
    if (moduleCatalog.IsHidden(id)) return Results.BadRequest(new { error = "Hidden services cannot be disabled." });
    if (enabled && !moduleCatalog.CanEnable(id)) return Results.Conflict(new { error = "A service cannot be enabled while its parent is disabled." });
    await states.SetEnabled(id, enabled);
    return Results.NoContent();
}).RequireAuthorization("ManageUsers").WithMetadata(new Microsoft.AspNetCore.Antiforgery.RequireAntiforgeryTokenAttribute());

app.Run();

static string? FindRepositoryModules(string start)
{
    for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
    {
        if (!File.Exists(Path.Combine(directory.FullName, "CompanyHQ.sln"))) continue;
        var modules = Path.Combine(directory.FullName, "src", "Web", "bin", "Debug", "net10.0", "modules");
        return Directory.Exists(modules) ? modules : null;
    }
    return null;
}

public partial class Program;
