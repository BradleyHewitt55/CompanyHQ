# Execute

Open work found during the plugin-discovery refactor session. Ordered by severity.
Excludes tooling (codex / agent wrappers). Each item names the file to change and why.

## P0 — build is red

- [x] **Remove stale `@using CompanyHQ.Services.Plugins`.**
  `Projects/Admin/Components/_Imports.razor:14` references the deleted composition
  project, so `Admin` does not compile. Delete the line.
  Verify: `dotnet build Projects/Admin/Admin.csproj` and `Projects/Cli/Cli.csproj`.

## P1 — Per-company process isolation

Replaces the three former P1 items (id collision, process-global routes/DI,
single-company fallback) with one architecture: they are all symptoms of one
process serving every tenant. This plan makes each company its own process.

**Model.** One Admin binary, one company per process, bound at startup by
`CompanyHq:Company`. Caddy routes each company domain to that company's process.
A process only ever loads its own services plus the shared built-ins; it never
opens another company's directory, so rebuilding one company cannot affect another.

**Build.** Each company's container image compiles *that company's* services at
`docker compose build` time. Rebuilding `admin-{company}` recompiles and picks up
newly scaffolded services for that company only. A host rebuild that adds a new
service therefore updates that company's services — and no other's.

**Paths & invariants (do not blur these):**
- *Source* — `MockServer/Companies/{company}/Services/*` lives in the build context.
- *Built binaries* — compiled into the image under `CompanyHq:PluginRoot`; the
  scanner loads these, never a volume copy. A stale volume cannot shadow them.
- *Runtime state* — source, DB credentials, and generated state live under the
  mounted `CompanyHq:ServerRoot`, which containers never scan for binaries.

### Steps

- [x] **1. Scanner — company-scoped.**
  `Projects/Infrastructure/PluginDiscovery/PluginAssemblyScanner.cs`
  Read `CompanyHq:Company`; throw at startup if unset. Keep the built-in scan of
  `AppContext.BaseDirectory`. Replace the `EnumerateDirectories(companiesRoot)`
  loop with a single `Companies/{company}/Services` scan — delete the all-companies
  loop and any plumbing that only existed to disambiguate tenants.
  The scanned `Companies/{company}/Services` path resolves from
  `CompanyHq:PluginRoot` (falling back to `ServerRoot` outside containers), so a
  runtime mount cannot shadow image-baked binaries.

- [x] **2. Catalog — single company, built-ins hidden from the list.**
  `Projects/Infrastructure/PluginDiscovery/PluginCatalog.cs`,
  `Projects/Abstractions/Plugins/IPluginCatalog.cs`
  `GetPlugins` returns only `PluginOrigin.Company` statuses; built-ins load and
  route but are not listed as company services. Drop the cross-company `Where`
  filter and the now-unused `company` parameter.

- [x] **3. Loader — collision check is now intra-process only.**
  `Projects/Infrastructure/PluginDiscovery/PluginLoader.cs`
  No change: a process loads one company + built-ins, so "same id, different
  assembly" now only fires on a real bug. Keep it fatal. Cross-tenant collision
  becomes structurally impossible, so `ServiceTemplate` id qualification (step 5)
  is traceability, not a crash fix.

- [x] **4. Company binding — set at startup, drop per-request resolution.**
  `Projects/Admin/Infrastructure/CompanyResolutionMiddleware.cs` (delete),
  `Projects/Admin/Program.cs`
  Initialize every scoped `ICompanyContext.Current` from `CompanyHq:Company`.
  Remove request-time resolution, the `Host`-header lookup, registry fallback,
  and middleware so Blazor circuit and background scopes are also bound.

- [x] **5. Scaffold id — qualify by company.**
  `Projects/Services/Companies/Services/ServiceTemplate.cs`, `ServiceScaffolder.cs`
  Generate id `company.{company}.{name}`; thread the company name through
  `ServiceScaffolder` into `ServiceTemplate.Plugin`.

- [x] **6. Dockerfile — host + per-company service build.**
  `Projects/Admin/Dockerfile`
  `sdk:10.0` build stage builds the Admin host and compiles the bound company's
  services (`Companies/{company}/Services/*`) against the shared `Abstractions`
  build, copying their DLLs and activation manifests into the immutable plugin
  root the scanner reads. A `COMPANY` build arg selects which company is compiled,
  so each image contains
  only its own services. `aspnet:10.0` runtime stage runs the host.
  Rebuild-to-update contract: because the services are compiled in and scanned
  from the image, `docker compose build admin-{company}` (rebuilding the host)
  is what refreshes that company's services and surfaces newly scaffolded ones —
  scoped to that company alone. Runtime state mounts target `ServerRoot`; scanned
  binaries remain under the separate, immutable `PluginRoot`.

- [x] **7. docker-compose — one service per company + caddy + postgres.**
  `docker-compose.yml`
  `admin-{company}` per dev company (alpha, beta): built with the `COMPANY` arg,
  env `CompanyHq:Company` + `CompanyHq:ServerRoot`, distinct internal port. A
  `caddy` service serving the Caddyfile. A `postgres` service (`companyhq`/
  `companyhq`) for the built-ins. Per-company mounts hold source, `db.json`, and
  generated state; service binaries come from the separate image plugin root, so
  a company rebuild is `docker compose build admin-{company}`.

- [x] **8. Caddyfile — per-company backends.**
  `MockServer/Caddyfile`
  `alpha.localhost → admin-alpha`, `beta.localhost → admin-beta` (one block each,
  distinct backend ports) instead of both domains sharing one backend.

- [x] **9. Docs.**
  Update the plugin/hosting docs to process-per-company, `CompanyHq:Company`, and
  the compose build flow. Note the source / built-binary / runtime-state split.

## P2 — correctness / architecture

- [x] **Scaffold template declares no dependencies.**
  `ServiceTemplate.cs` emits `PluginDescriptor` with `Dependencies: []`, but company
  services use `[Authorize]` and controllers — they depend on Companies, Postgress,
  and Auth. Harmless only because those are `Required`; the ordering graph does not
  guarantee built-ins configure first. Emit
  `["companyhq.companies","companyhq.postgress","companyhq.auth"]`.

- [x] **`AddDbContext<DbContext>` registers the EF base type.**
  `Projects/Services/Postgress/PostgressPlugin.cs:34` — a second data provider plugin
  would collide on the same registration. Use a derived context per provider.

- [x] **Docs still describe the deleted composition project.**
  `Docs/README.md`, `Docs/Architecture/Overview.md`, `Docs/Architecture/Plugins.md`,
  `Docs/Architecture/AdminHost.md`, `Docs/Reference/WorkspaceLayout.md`,
  `Docs/Reference/Cli.md`, `Docs/GettingStarted.md` reference `Services/Plugins`,
  `CompanyHqPlugins`, `AddCompanyHqPlugins`, `UseCompanyHqPlugins`. Rewrite for
  `Projects/Infrastructure/PluginDiscovery`, `AddPluginDiscovery`,
  `UseDiscoveredPlugins`, `IPluginCatalog`, `PluginDescriptor`, and
  dependency-ordered discovery. Verify every doc link still resolves.

- [x] **Integration fixture wipes the whole `MockServer/Companies` tree.**
  `Projects/Tests/IntegrationTests/PostgresFixture.cs:26-29,91-94` deletes
  `Companies/` wholesale on setup/teardown, destroying any hand-made dev company.
  Scope teardown to the companies the fixture created.

## P3 — cleanup / cosmetic

- [x] **Delete the dead template home page.**
  `Projects/Admin/Components/Pages/Home/` — `Home.razor` is an empty comment stub;
  `Dashboard.razor` owns `/` and `/admin`. Remove the folder.

- [x] **`NavMenu` links to routes that do not exist.**
  `Projects/Admin/Components/Layout/Components/NavMenu.razor` — `pipeline`, `users`,
  `gallery`, `keys`, `skills`, `agents`, `help`, `settings` all 404. Wire or remove.

- [x] **`AdminChrome` shows a hardcoded user.**
  `Projects/Admin/Components/Layout/Components/AdminChrome.razor:16` — "Olivia Martin"
  / "OM" is static markup. Bind to `ICurrentUser`.

- [x] **Doc note: plugin-scan assembly-identity trap.**
  `PluginAssemblyScanner` loads every base-dir DLL and `GetTypes()`; a company
  service built against a different `Abstractions.dll` copy fails `IsAssignableFrom`
  and is silently skipped. Document the requirement (shared Abstractions build).
