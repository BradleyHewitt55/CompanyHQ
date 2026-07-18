# GOAL

Build the initial CompanyHQ framework: a Blazor SSR admin shell (AdminUI) that discovers and renders pluggable services, with Auth and Menu as the first two built-in services.

# Architecture Decisions (agreed — do not re-litigate)

- **AdminUI** (`src/AdminUI/`) is a Blazor SSR project. It is a thin shell: layout, routing, service discovery. No business logic.
- **Services** live in `src/Services/{ServiceName}/`. Built-in and user-created services share the same directory and the same template. Every service is a Razor Class Library (RCL).
- **Components** (`src/Components/`) is a shared RCL holding *only* generic, domain-free UI (Button, Table, …) and `ServiceComponentBase`. No interfaces, no contracts, nothing that isn't a renderable component.
- **Abstractions are split by owner, not dumped in one project** (OrchardCore pattern: interface and implementation live in sibling projects, so consumers depend on the contract, never on the concrete code):
  - `src/Abstractions/` — framework-level, owned by no single service: `IServiceModule`, `ComponentRegistry`. Implemented/driven by AdminUI itself.
  - `src/Services/Auth.Abstractions/` — `IPermissionChecker`. Implemented by `Auth` (`PermissionService`). Menu and AdminUI reference this project only, never `Auth` directly.
  - `src/Services/Core.Abstractions/` — `ICompanyContext`, `ICompanyDbFactory`, `BaseModel`, `IPostgresService`. `ICompanyContext`/`ICompanyDbFactory` implemented by `Core` (`CompanyDbFactory`). `IPostgresService` is the DB-specific data-access contract — implemented by a `PostgresService` (in `Core`), never by `Core.Abstractions` itself. Any service needing company context or data access references this project only, never `Core` directly.
  - Rule of thumb: if a project only wants to *call* an interface, it references the `.Abstractions` project. If it *implements* the interface, it references `.Abstractions` and provides the concrete type. Nobody except the owning service references the concrete implementation project of another service.
- AdminUI discovers services by scanning referenced assemblies for `IServiceModule` implementations and registers their routable components via `AddAdditionalAssemblies`.
- Service pages render under `/services/{service}/...`; settings pages under `/services/{service}/settings/...`.
- **Company isolation**: each company gets its own database. The user supplies a connection string at signup. Connection strings are stored encrypted in the platform DB and are never exposed to services — services access data only through `IPostgresService` (from `Core.Abstractions`), which resolves the scoped `DbContext` internally. No raw SQL access for users, ever.
- **Auth data lives in the platform DB**, not company DBs (you must authenticate before you know which company DB to use). Company-scoped roles/permissions may live per company later.
- **Menu visibility = authorization.** The Menu service stores nothing. Menu items carry a permission key (default: the service name); the Auth service stores role → permission grants; `MenuService` filters the aggregated menu by asking `IPermissionChecker` (from `Auth.Abstractions`). Never store menu visibility separately — it would drift from page access and become a security bug.
- **Component/page overriding** goes through `ComponentRegistry` (name → type). For the initial build this is a simple registration at startup — built-ins register once, first and only registration wins. There is no user-assembly override yet; that only becomes relevant once Explorer (see below) can generate services of its own. Rendering uses `DynamicComponent` via the registry. Do not rely on duplicate `@page` routes (Blazor throws on those).
- **Admin/Client DTOs**: each entity gets `Admin{Entity}Dto` and `Client{Entity}Dto`, including Auth's `User` entity. The website/client host doesn't exist yet in this build (out of scope, see below), so `Client{Entity}Dto` has no consumer wired up yet — it still gets created alongside the Admin one every time, cheap now, expensive to retrofit later. Host projects alias the plain entity name globally in user-owned imports files: `@using Product = Products.Dtos.AdminProductDto` in AdminUI's `_Imports.razor`, the Client DTO on the website side (once it exists); same aliases in a `GlobalUsings.cs` for the C# side. The user does not get Blazor's default imports — the imports files are fully user-owned and start with only what the template exposes. Neither DTO ever carries Identity internals (`PasswordHash`, `SecurityStamp`, `ConcurrencyStamp`) — both are built from an explicit allow-list of fields (e.g. `Id`, `UserName`, `Email`, `Roles`), never a raw projection of the entity.
  - **Exception: `Company` and any other platform/multi-tenant model** (Core's tenancy plumbing — connection strings, tenant metadata) gets **no** `Client{Entity}Dto`. These describe the platform itself, not something a company's own end users ever see; only `AdminCompanyDto` exists, consumed by AdminUI/platform-admin surfaces. A `Client{Entity}Dto` here would be a DTO with no legitimate reader and a standing risk of leaking tenant internals.
- **Foundational services** (Auth, Menu, Core) are marked non-removable. Every service compile-references Menu (for the `MenuItem` type); Menu references no service back, so no cycles.

## Service template

Every service — built-in, hand-written, or generated — has exactly this shape:

```
src/Services/{ServiceName}/
    {ServiceName}Module.cs   // implements IServiceModule (name, description, DI registration)
    MenuItems.cs             // this service's MenuItem tree (parent + children)
    Models/                  // classes saved to the DB
    Dtos/                    // Admin{Entity}Dto / Client{Entity}Dto return values
    Api/                     // controllers
    Services/                // business logic
    Components/              // service-specific components others can pull in
    Pages/                   // default front-facing UI
    Settings/                // settings pages (e.g. the service details page)
```

Services come in three flavors, all with the same shape: page services (most), component services (Menu — `Pages/`/`Settings/` empty), and pure logic services. Empty folders stay in the template.

A service that exposes an interface for others to consume additionally gets a sibling `{ServiceName}.Abstractions` project holding just that interface (see Auth, Core below). Not every service needs one — only ones other services must call into.

## Menu rules

- `MenuItem` record: `Title`, `Url`, `Permission` (defaults to service name), `Children`.
- Each service declares its items in its own `MenuItems.cs` — the service is the sole author of its entries; no other project is touched when a service is added or removed.
- The parent item has its own URL and is the default navigation target on click (usually the list page). If the parent URL equals a child URL, that child renders active when the parent is opened.
- `MenuService` (in the Menu service, stateless): aggregate all declared items via the module scan → filter through `IPermissionChecker` (from `Auth.Abstractions`) → return the tree. `MenuItems.razor` (in `Menu/Components/`) renders whatever tree it is handed. AdminUI's layout pulls `MenuItems.razor`.

# Requirements (original)

## Layout

- The layout of the admin UI is `ExamplePages/company-dashboard.html`. Colors come from the company primary and secondary color.

## Auth

- Use ASP.NET Core Identity for the auth service.
- Requires an API to connect to the DB.

## Database

- Users cannot run raw SQL. They access data only through built services (sqlite/pg run as standalone services).

## Admin UI — Explorer (later phase, not in initial build)

Not built in this pass, but the initial architecture must not preclude it — do not design module scanning or the component registry in a way that would need reworking later.

- Users see their services and pages; clicking a page shows a live view.
- Users can add a **service** from the Explorer by name; it scaffolds the standard template on disk (Explorer-scaffolded services get their `MenuItems.cs` written by the generator).
- Users can also add a **page** within an existing service from the Explorer — scaffolds a new file under that service's `Pages/` (or `Settings/`) and, if it should appear in navigation, an entry the generator writes into that service's `MenuItems.cs`.
- Either way, the editor body is where the user works afterward: a code view (the file(s) just scaffolded) and a live-page view (the rendered page), toggle between them — code editing is built later, but the addition flow must produce something for it to open.
- Client sees only relevant directories.
- `ComponentRegistry`'s override-by-name resolution (a user/generated component replacing a built-in) only becomes relevant once Explorer exists to generate those components.

# Implementation Steps (initial build)

Do these in order. Each phase must build (`dotnet build`) before starting the next.

## Phase 1 — Solution skeleton

1. Create projects and add all to `CompanyHQ.sln`:
   - `src/AdminUI/AdminUI.csproj` — Blazor Web App, SSR (server render mode, no WebAssembly).
   - `src/Abstractions/Abstractions.csproj` — plain class library (framework-level contracts, owned by no service).
   - `src/Components/Components.csproj` — Razor Class Library.
   - `src/Services/Core.Abstractions/Core.Abstractions.csproj`, `src/Services/Auth.Abstractions/Auth.Abstractions.csproj` — plain class libraries.
   - `src/Services/Core/Core.csproj`, `src/Services/Auth/Auth.csproj`, `src/Services/Menu/Menu.csproj` — Razor Class Libraries.
2. References:
   - `Abstractions`, `Core.Abstractions`, `Auth.Abstractions` — leaf projects, no references out.
   - `Components` → `Abstractions` + `Core.Abstractions` (for `ServiceComponentBase`, which injects `ICompanyContext` and `ComponentRegistry`).
   - `Menu` → `Components`, `Abstractions`, `Auth.Abstractions` (calls `IPermissionChecker`; never references `Auth`).
   - `Auth` → `Components`, `Abstractions`, `Auth.Abstractions` (implements `IPermissionChecker`), `Menu` (for `MenuItem`).
   - `Core` → `Components`, `Abstractions`, `Core.Abstractions` (implements `ICompanyContext`/`ICompanyDbFactory`/`IPostgresService`), `Menu` (for `MenuItem`).
   - `AdminUI` → `Abstractions`, `Components`, `Core.Abstractions`, `Auth.Abstractions`, and all service projects (`Menu`, `Auth`, `Core`).
3. Strip AdminUI's default `_Imports.razor` down to the template-exposed set (Components, abstractions, aliases) — no hidden defaults. Add an empty `GlobalUsings.cs`.

## Phase 2 — Abstractions projects

1. `src/Abstractions/IServiceModule.cs`: `Name`, `Description`, `RegisterServices(IServiceCollection)`.
2. `src/Abstractions/ComponentRegistry.cs`: `Register(string name, Type type)` (last wins), `Resolve(string name)`.
3. `src/Services/Core.Abstractions/ICompanyContext.cs` (current company id + name) and `ICompanyDbFactory.cs` (`CreateAsync<TContext>()`).
4. `src/Services/Core.Abstractions/BaseModel.cs`: plain marker base (`Id`) that every service's `Models/*` entity inherits — just the shape, no DB-specific logic.
5. `src/Services/Core.Abstractions/IPostgresService.cs`: the DB-specific data-access contract, generic over `TModel : BaseModel` (`GetAllAsync<TModel>()`, `GetByIdAsync<TModel>(Guid)`, `AddAsync<TModel>(TModel)`, `UpdateAsync<TModel>(TModel)`, `DeleteAsync<TModel>(Guid)`). This interface lives here because it's what services depend on to call in; its implementation (`PostgresService`, Postgres-specific) lives in `Core`, not here.
6. `src/Services/Auth.Abstractions/IPermissionChecker.cs`: `Task<bool> HasAccessAsync(string permissionKey)`.

No implementations here — these projects contain interfaces and the registry only.

## Phase 3 — Components project

1. Generic UI, one folder per component holding its UI + any component-local service: `Button/Button.razor`, `Table/Table.razor` (generic `TItem`, `TableOptions` parameter object designed for subclassing, `RenderFragment` column templates).
2. `ServiceComponentBase.cs`: base component injecting `ICompanyContext` (from `Core.Abstractions`) and `ComponentRegistry` (from `Abstractions`).
3. Default service details page template (name, description, enable/disable toggle) registered in the registry under a well-known name so services get it for free and can override by name.

## Phase 4 — Auth service

Built before Menu so Menu's permission filtering has real roles/grants to filter against by the time it's implemented (Phase 5), and so Phase 8 verification isn't stalled waiting on data that doesn't exist yet.

This service must be **production-grade, not a dev stub** — it is the security boundary for the whole platform.

1. ASP.NET Core Identity against the **platform DB** (SQLite acceptable for local dev; provider swappable for prod). Identity's default password hasher (PBKDF2) and lockout options are used as-is — no custom/weaker hashing. Enforce a real password policy (length, complexity, lockout after N failed attempts) via Identity options, not defaults left unconfigured.
2. `Models/`: Identity user + `Role` + `RolePermission` (role → permission key grants).
3. `Services/PermissionService.cs` implementing `IPermissionChecker` (from `Auth.Abstractions`) from the current user's roles. Register it against the `Auth.Abstractions` interface in DI so consumers never see the concrete type. Denies by default: a permission key with no matching grant returns `false`, never `true`.
4. `Api/`: minimal auth endpoints (login, logout) — Identity needs API access to the DB per requirements. Anti-forgery/CSRF protection on state-changing endpoints, rate-limited login attempts, no verbose error messages that leak whether a username exists.
5. `Pages/Login.razor` (outside the authorized layout).
6. `Settings/`: role management page — assign permission keys (service names) to roles. This is where "hide menu items from staff" is actually configured.
7. Seed a single default admin user + role with grants covering Auth/Core/Menu's permission keys, so Phase 8's manual verification has something to log in with. No second seeded user — restricted-role users are created and tested live in Phase 8, not pre-seeded.
8. `MenuItems.cs`: parent "Auth" → children Users, Roles.
9. `AuthModule.cs` registering services.

Note: this phase compile-references `Menu` (for the `MenuItem` type in step 8), but Menu's own logic isn't implemented until Phase 5 — the project exists and builds from Phase 1, only its behavior is filled in later.

## Phase 5 — Menu service (proves the template)

1. Scaffold the full template shape (empty `Models`-as-entities, `Dtos`, `Api`, `Pages`, `Settings` folders stay).
2. `Models/MenuItem.cs`: the record above. Not a DB entity — Menu is stateless.
3. `MenuItems.cs` declaring nothing (Menu has no menu entries of its own) — file exists to honor the template.
4. `Services/MenuService.cs`: collect `MenuItems` from all discovered modules, filter via `IPermissionChecker` (injected from `Auth.Abstractions`), return the tree. No caching — rebuild the tree per request.
5. `Components/MenuItems.razor`: renders a tree it receives as a parameter; implements the parent/child active-URL rule. No data fetching inside the component beyond calling `MenuService`.
6. `MenuModule.cs` registering `MenuService`.

## Phase 6 — Core service (companies)

1. `Models/Company.cs`: id, name, description, primary/secondary color, encrypted connection string, enabled flag. Lives in the platform DB.
2. `Services/CompanyService.cs`: create (validate the supplied connection string by opening a connection), update, soft-delete. Encrypt connection strings with ASP.NET Core Data Protection, configured as follows (the production-appropriate setup, not the easiest one): `PersistKeysToDbContext` (Microsoft.AspNetCore.DataProtection.EntityFrameworkCore) to store the key ring in the platform DB — durable across restarts, shared across multiple instances without extra infra — **and** `ProtectKeysWithCertificate` so the key ring itself is encrypted at rest rather than sitting in the DB in plaintext XML. No local-disk key folder, no unprotected key ring, anywhere.
3. `Dtos/`: `AdminCompanyDto` only (see the DTO exception above — no `ClientCompanyDto`; company/tenant data is never returned to end users).
4. `Services/CompanyDbFactory.cs` implementing `ICompanyContext` and `ICompanyDbFactory` (from `Core.Abstractions`): resolve current company, decrypt its connection string, return a scoped `DbContext` on it. Internal to Core — services never call this directly.
5. `Services/PostgresService.cs` implementing `IPostgresService` (from `Core.Abstractions`): generic CRUD over EF Core using the `DbContext` resolved via `CompanyDbFactory` internally. Register against the `Core.Abstractions` interface in DI so consumers depend only on `IPostgresService`, never on `CompanyDbFactory`/`DbContext` directly — this is the only data-access surface services get; no raw SQL, no exposed `DbContext`.
6. Deletion requires a confirmation flow: type-the-company-name confirmation component, soft-delete first; hard purge is a separate explicit action.
7. `Pages/Signup.razor`: single form, single submit — company name, description, primary/secondary color, and PG connection string collected together on one page. On submit, `CompanyService.CreateAsync` validates the connection string by opening a connection, then persists the company row (string encrypted) in one transaction; reject the whole submission if the connection fails. `Pages/`: also a company list page.
8. `Settings/`: company details.
9. `MenuItems.cs` + `CoreModule.cs`. Mark Core, Auth, Menu as non-removable.

## Phase 7 — AdminUI shell

1. Port `ExamplePages/company-dashboard.html` into the main layout; wire primary/secondary colors from the current company (CSS custom properties).
2. Startup: scan referenced assemblies for `IServiceModule`, call each `RegisterServices`, populate `ComponentRegistry` (built-ins first), `AddAdditionalAssemblies` for routable pages.
3. Sidebar area pulls `MenuItems.razor` from the Menu service.
4. Route protection: pages require auth by default; access to `/services/{service}/...` checked against `IPermissionChecker` (from `Auth.Abstractions`) with the service's permission key. Login page anonymous.
5. Services overview page listing modules with their details page (from the registry) and enable/disable.

## Phase 8 — Verify

1. `dotnet build` clean; run AdminUI.
2. Manual pass: log in with the seeded admin → sidebar shows Core/Auth/Menu entries → sign up a company with a PG connection string on the single signup page → soft-delete a company through the confirmation flow.
3. Live permission test (no pre-seeded second user): through the admin's Users/Roles pages, create a new role granted only a subset of permission keys, create a new user, assign them that role, then log in as that user and confirm — for every permission key **not** granted — the corresponding menu item does not render **and** the corresponding `/services/{service}/...` URL is blocked (not just hidden; a direct navigation must be rejected).
4. Security verification, run via a subagent tasked with actively trying to break the Auth service: attempt privilege escalation (tampering with claims/tokens, calling permission-gated endpoints directly without the granting role), brute-force/lockout behavior on login, and any path that could bypass `IPermissionChecker`. Also confirm no code path anywhere in the services reaches a `DbContext`/raw SQL connection directly instead of going through `IPostgresService` — grep the solution for direct `DbContext`/connection-string usage outside `Core`, and confirm no other service can query the platform DB directly. Findings get fixed before this phase is considered done.

Component-registry override behavior is not part of this verification pass — there's nothing to override until Explorer exists (see Explorer notes above).

Out of scope for the initial build: Explorer, website/client host, runtime service creation, per-company role storage, sqlite/pg standalone DB services.
