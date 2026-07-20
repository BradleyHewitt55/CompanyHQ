# Plugins

## Decision

Every service is a plugin: a headless project implementing [`ICompanyHqPlugin`][1]
that any host can load and consume.

- **Razor Class Libraries by convention.** All plugin projects use
  `Microsoft.NET.Sdk.Razor` + `AddRazorSupportForMvc`, even ones that look
  headless (e.g. the [`Postgress`][2] provider): plugins ship Razor pages that the
  consuming host inherits and can extend. Never downgrade a plugin's SDK to
  plain `Microsoft.NET.Sdk`.
- **A plugin owns its whole contribution**, not just its DI registrations:
  - [`ConfigureServices`][1] registers the plugin's services.
  - [`ConfigureApplication`][1] registers the middleware those services require.
    A plugin that registers authentication also calls `UseAuthentication`; the
    host is never expected to know a plugin's private pipeline needs.
  - [`RouteAssemblies`][1] declares the assemblies holding routable components,
    so the host wires pages up without naming the plugin's types.

  Both `ConfigureApplication` and `RouteAssemblies` have defaults, so a
  service-only plugin still implements one method.
- **`RootNamespace` must be set on every plugin project.** Without it, generated
  Razor types land in `{AssemblyName}.*` while the C# lands in
  `CompanyHQ.Services.{Name}.*`, and consumers have to import both. See
  [`Auth.csproj`][6].
- **One plugin list, one order.** [`CompanyHqPlugins`][3] is the single registry;
  the Admin host, the CLI, and the Blazor router all read it. Hosts never
  construct plugin instances. Order is meaningful and documented there: Companies
  first (it owns paths and tenant context), then the data provider, then Auth.
- **Middleware ordering is a contract, not a convention.** Hosts call
  `UseCompanyHqPlugins` at one slot: *after* the company is resolved, *before*
  antiforgery and endpoint mapping. Plugin middleware may assume
  [`ICompanyContext.Current`][4] is already set.
- **No hot reload**: enabling/adding a plugin recycles the app; startup discovery
  does the rest (see [DataAccess](DataAccess.md)).

### Not yet built

Discovery. [`CompanyHqPlugins.Default`][3] is a hardcoded list of compile-time
project references — the contract is real, the loader is not. A scaffolded service
under `Companies/{Name}/Services/` compiles as a plugin but nothing loads it yet.
See [Service scaffolding](ServiceScaffolding.md).

## Host wiring

```csharp
builder.Services.AddCompanyHqPlugins(builder.Configuration);

// ...
app.UseMiddleware<CompanyResolutionMiddleware>();  // tenant first
app.UseCompanyHqPlugins();                         // then plugin middleware
app.UseAntiforgery();
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(CompanyHqPlugins.RouteAssemblies);
```

Non-web hosts call `AddCompanyHqPlugins` only; [the CLI][5] never invokes
`ConfigureApplication`, which is why it defaults to a no-op.

See [Overview](Overview.md) for how plugins sit in the wider system.

[1]: ../../Projects/Abstractions/ICompanyHqPlugin.cs
[2]: ../../Projects/Services/Postgress/PostgressPlugin.cs
[3]: ../../Projects/Services/Plugins/CompanyHqPlugins.cs
[4]: ../../Projects/Abstractions/Companies/ICompanyContext.cs
[5]: ../../Projects/Cli/Program.cs
[6]: ../../Projects/Services/Auth/Auth.csproj
