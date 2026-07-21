# Plugins

## Decision

Every service is a headless [`ICompanyHqPlugin`][1] discovered at process startup and ordered by its declared dependencies.

- **One company per process.** [`PluginAssemblyScanner`][2] requires `CompanyHq:Company`, loads built-ins from the host directory, and scans only that company's `Companies/{company}/Services` directory.
- **Built-ins stay hidden.** Built-ins remain active and routable, while [`IPluginCatalog.GetPlugins()`][3] reports only company services.
- **Activation is image-defined.** A company service is active when its image-baked `Settings/service.json` contains `"enabled": true`. Changing the source manifest requires `docker compose build admin-{company}` and recreating that service; plugins are never loaded into a running process.
- **Dependencies determine order.** [`PluginLoader`][4] validates IDs, orders active plugins, calls `ConfigureServices`, and later calls `ConfigureApplication` through `UseDiscoveredPlugins`.
- **Plugin binaries are immutable.** Containers scan `CompanyHq:PluginRoot`; mutable company source, `db.json`, and generated state live under `CompanyHq:ServerRoot`. A runtime mount therefore cannot shadow rebuilt assemblies.
- **Abstractions has one assembly identity.** Every company service must compile against the same `Projects/Abstractions/Abstractions.csproj` build as the host. A separately built or copied `Abstractions.dll` can have a different runtime identity, causing scanner type checks against `ICompanyHqPlugin` to reject the service. The Admin image enforces this by compiling company services against its shared Abstractions build.
- **Razor routes are declared.** `RouteAssemblies` supplies routable plugin assemblies to the Admin host without compile-time knowledge of company services.

## Host wiring

```csharp
builder.Services.AddPluginDiscovery(builder.Configuration);

var app = builder.Build();
app.UseDiscoveredPlugins();
app.UseAntiforgery();
app.MapControllers();

var catalog = app.Services.GetRequiredService<IPluginCatalog>();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(catalog.RouteAssemblies.ToArray());
```

Every scoped `ICompanyContext` is initialized from `CompanyHq:Company` by the Companies plugin before plugin middleware or components resolve it.

[1]: ../../Projects/Abstractions/Plugins/ICompanyHqPlugin.cs
[2]: ../../Projects/Infrastructure/PluginDiscovery/PluginAssemblyScanner.cs
[3]: ../../Projects/Abstractions/Plugins/IPluginCatalog.cs
[4]: ../../Projects/Infrastructure/PluginDiscovery/PluginLoader.cs
