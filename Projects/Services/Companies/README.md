# Companies

This guide is for hosts that need tenants: creating companies, resolving the
current one, and scaffolding services inside it.

## Directory responsibilities

| Directory | Responsibility |
|---|---|
| `Services/` | Headless implementation. [`CompanyRegistry`][1] (filesystem-as-registry), [`CompanyCreation`][2] (interactive tenant setup), [`ServerPaths`][3] (root resolution), [`ServiceScaffolder`][4] and [`ServiceTemplate`][5] (code generation), [`ServiceCatalog`][6] (listing), [`CompanyLocator`][7] (directory → company), [`CompanyContext`][8] (ambient tenant), [`NameValidator`][9]. |

Root files define service integration: [`CompaniesPlugin`][10] registers
dependencies; [`Companies.csproj`][11] defines the service package.

This plugin owns no pages and no middleware — it implements only
`ConfigureServices`.

## I only need the default setup

Load it through the shared registry; it must come first, because the other
plugins resolve paths and the tenant context it provides:

```csharp
builder.Services.AddCompanyHqPlugins(builder.Configuration);
```

Point it at a server root. Production defaults to `~/CompanyHQ`:

```json
{
  "CompanyHq": {
    "ServerRoot": "C:/path/to/CompanyHQ/MockServer"
  }
}
```

## What each contract gives you

| Contract | Lifetime | Use it for |
|---|---|---|
| [`ICompanyRegistry`][12] | Singleton | Listing and registering companies |
| [`ICompanyContext`][13] | **Scoped** | Reading the current tenant |
| [`IServerPaths`][14] | Singleton | Resolving `Companies/{Name}` on disk |
| [`IServiceCatalog`][15] | Scoped | Listing the current company's services |
| [`IServiceScaffolder`][16] | Scoped | Generating a new service |
| [`ICompanyLocator`][17] | Singleton | Deriving a company from a directory path |

[`ICompanyContext`][13] is **scoped on purpose**: a singleton would leak one
tenant's selection into another's work.

## I want to read the current company

Inject [`ICompanyContext`][13] and read `Current`. `null` means platform scope —
shared CompanyHQ state belonging to no company.

```csharp
@inject ICompanyContext CompanyContext
```

**Never assign `Current` from a component.** The host's
[`CompanyResolutionMiddleware`][18] owns that value. On an interactive Blazor
circuit the DI scope outlives the request, so a component that writes it
re-points the tenant for everything downstream on that circuit.

## I want to list or add a service

Use [`IServiceCatalog`][15] and [`IServiceScaffolder`][16] — both act on the
ambient company, so neither takes a company argument:

```csharp
@inject IServiceCatalog Catalog
@inject IServiceScaffolder Scaffolder

services = await Catalog.ListAsync();
await Scaffolder.ScaffoldAsync("Billing");
```

`ScaffoldAsync` throws `ArgumentException` for an invalid name,
`InvalidOperationException` for a duplicate or when no company is selected. See
[Service scaffolding](../../../Docs/Architecture/ServiceScaffolding.md) for the generated
layout.

## I want to create a company

Interactively, through the CLI — see [Companies architecture](../../../Docs/Architecture/Companies.md):

```powershell
dotnet run --project Projects/Cli/Cli.csproj -- --c "Acme" --root "$PWD/MockServer"
```

Programmatically, `RegisterAsync` creates the directories only; database
provisioning and admin seeding belong to [`CompanyCreation`][2].

## Verify it worked

```powershell
dir MockServer/Companies/Acme/Services
```

The company directory exists with a `Services/` folder. After scaffolding, the
service directory contains `{Name}.csproj` and `{Name}Plugin.cs`.

## Common mistakes

- **Assigning `ICompanyContext.Current` from UI.** Middleware owns it; see above.
- **Walking the filesystem to list services.** Use [`IServiceCatalog`][15], or the
  logic drifts between the CLI and the Admin explorer.
- **Registering `ICompanyContext` as a singleton.** It is scoped so tenants stay
  isolated per request and per circuit.
- **Expecting `RegisterAsync` to produce a usable tenant.** It creates directories.
  A tenant is usable after the database exists and an admin is seeded.
- **Expecting a scaffolded service to load.** It compiles; nothing discovers it
  yet. See [Plugins](../../../Docs/Architecture/Plugins.md).

[1]: Services/CompanyRegistry.cs
[2]: Services/CompanyCreation.cs
[3]: Services/ServerPaths.cs
[4]: Services/ServiceScaffolder.cs
[5]: Services/ServiceTemplate.cs
[6]: Services/ServiceCatalog.cs
[7]: Services/CompanyLocator.cs
[8]: Services/CompanyContext.cs
[9]: Services/NameValidator.cs
[10]: CompaniesPlugin.cs
[11]: Companies.csproj
[12]: ../../Abstractions/Companies/ICompanyRegistry.cs
[13]: ../../Abstractions/Companies/ICompanyContext.cs
[14]: ../../Abstractions/Companies/IServerPaths.cs
[15]: ../../Abstractions/Companies/IServiceCatalog.cs
[16]: ../../Abstractions/Companies/IServiceScaffolder.cs
[17]: ../../Abstractions/Companies/ICompanyLocator.cs
[18]: ../../Admin/Infrastructure/CompanyResolutionMiddleware.cs
