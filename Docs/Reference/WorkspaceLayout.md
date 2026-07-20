# Workspace layout

One bullet per project: what it is, and the constraint that trips people up.

## Projects

- [`Projects/Abstractions`][1]: every contract in the system — [`ICompanyHqPlugin`][2],
  the `Companies/`, `Data/`, and `Users/` interfaces. **Depends on nothing but
  provider-neutral EF Core**, so plugins can map entities without pulling in a
  database driver. Nothing here may reference a service project.
- [`Projects/Services/Plugins`][3]: the headless plugin composition service containing the single registry ([`CompanyHqPlugins`][3]) plus
  the `AddCompanyHqPlugins` / `UseCompanyHqPlugins` extensions. **The only project
  that names plugins.** Hosts reference this instead of the services.
- [`Projects/Services/Companies`][4]: tenants — registry, paths, context, scaffolding,
  catalogue, locator. **Must be registered first**; the others resolve against it.
  See [Companies](../../Projects/Services/Companies/README.md).
- [`Projects/Services/Postgress`][5]: the persistence provider. **Loading it is the
  provider choice** — consumers only ever see `Abstractions.Data`. See
  [Postgress](../../Projects/Services/Postgress/README.md).
- [`Projects/Services/Auth`][6]: company-bound login, registration, authorization,
  and the pages and layout for them. **The reference layout every scaffolded
  service copies.** See [Auth](../../Projects/Services/Auth/README.md).
- [`Projects/Admin`][7]: the Blazor Server host. **Owns the pipeline and shell only**;
  it names no plugin. See [Admin host](../Architecture/AdminHost.md).
- [`Projects/Cli`][8]: the `companyhq` executable. **A headless host** — it calls
  `AddCompanyHqPlugins` but never `UseCompanyHqPlugins`. See [CLI](Cli.md).
- [`Projects/Tests/UnitTests`][9]: no database, no host. Filesystem tests use temp
  directories.
- [`Projects/Tests/IntegrationTests`][10]: **requires the local PostgreSQL container**
  (`companyhq/companyhq` on `localhost:5432`) and uses `MockServer/` as the server root.

## Other directories

- `MockServer/`: the development server root. Mirrors the production `~/CompanyHQ`
  layout exactly, so the same code runs against both.
- `Docs/`: this documentation.
- `Agents/`: headless CLI agents. **Used only when explicitly requested** — see
  [CLI agents](../Architecture/CliAgents.md).
- `.repos/`: reference checkouts of external documentation. Do not edit.

## Dependency direction

```
Admin ─┐
       ├─> Hosting ─> Services/{Companies,Postgress,Auth} ─> Abstractions
Cli  ──┘
```

Nothing flows the other way. A service never references a host, and Abstractions
never references a service.

[1]: ../../Projects/Abstractions/Abstractions.csproj
[2]: ../../Projects/Abstractions/ICompanyHqPlugin.cs
[3]: ../../Projects/Services/Plugins/CompanyHqPlugins.cs
[4]: ../../Projects/Services/Companies/Companies.csproj
[5]: ../../Projects/Services/Postgress/Postgress.csproj
[6]: ../../Projects/Services/Auth/Auth.csproj
[7]: ../../Projects/Admin/Program.cs
[8]: ../../Projects/Cli/Program.cs
[9]: ../../Projects/Tests/UnitTests/UnitTests.csproj
[10]: ../../Projects/Tests/IntegrationTests/IntegrationTests.csproj
