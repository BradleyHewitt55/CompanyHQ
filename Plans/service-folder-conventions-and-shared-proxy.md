# Service folder conventions, settings pages, and the shared proxy

Wave 1. Tasks are file-disjoint and may run as parallel subagents; each agent owns exactly one task section below and ticks only its own checkboxes.

## Task: Auth service-folder convention

### Goal

Move Auth injectable classes into `Services/` using the established service naming and namespace convention.

### Scope

- [x] Move and rename PasswordGenerator, UserDirectory, AdminUserSeeder, and LoginRateGate to `Services/*Service.cs`.
- [x] Move CommonPasswordValidator to `Services/` without changing its framework-conventional type name.
- [x] Move AuthRoles to `Models/`.
- [x] Update DI, endpoint signatures, pages, imports, and tests.

### Task-specific tests

- [x] Build Auth and run its tests after the renames.
- [x] Grep for obsolete Auth type names outside their replacement files.
- [x] Exercise owner bootstrap and admin user endpoints to verify their updated dependencies resolve.
- [x] Assert AuthRoles resolves from `Services.Auth.Models`.

## Task: Postgres service-folder convention

### Goal

Move Postgres injectable service classes into `Services/` without changing startup, model seeding, or database behavior. Must land before the wave-2 database work (same files).

### Scope

- [x] Move and rename ServiceStateStore, PostgresService, ModelExtensionService, RelatedRecordsService, and DatabaseSchemaMigrator.
- [x] Keep infrastructure classes in their current locations.
- [x] Confirm the namespace filter in Postgres startup still excludes only Companies contract metadata.

### Task-specific tests

- [x] Build Postgres and run its tests after the moves.
- [x] Assert service-state DI resolves the renamed implementation.
- [x] Start against a company database and verify entity-definition seeding remains unchanged.
- [x] Grep for obsolete Postgres type names outside their replacement files.

## Task: Service scaffold folder guarantee

### Goal

Guarantee every user-created service includes the full convention tree, including `Services/`.

### Scope

- [x] Preserve `Services/` creation in `ServiceScaffolder.Generate`.
- [x] Preserve `Services/` folders in company blueprint trees.

### Task-specific tests

- [x] Generate a service and assert all six convention folders exist, including `Services/`.
- [x] Assert blueprint trees retain their `Services/` directories.

## Task: Service settings pages

### Goal

Wire module settings pages into the admin UI with server-side authorization and enabled-service gating.

### Scope

- [x] Register `IServiceModule.SettingsPages` in `ComponentRegistry` as `services/{id}/settings` and named subroutes.
- [x] Gate settings pages by effective service enablement.
- [x] Add Settings links to eligible service cards.
- [x] Require the `ManageUsers` policy at the settings route.

### Task-specific tests

- [x] Assert a settings component resolves only while its service is enabled.
- [x] Disable the service and assert its settings route returns 404.
- [x] bUnit-test Settings link visibility for modules with and without settings pages.
- [x] Assert a Viewer receives server-side denial at a settings route.

## Task: Shared proxy topology

### Goal

Run one server-level reverse proxy for all companies; company applications must be private to the Docker network. Must land before the wave-2 database work (both edit Compose generation).

**Binding decisions (2026-07-15, also recorded in ROADMAP.md Log — read before implementing):** the shared proxy is owned by CompanyHQ itself. It is a service in the server-level `{server-root}/CompanyHQ/docker-compose.yml` — the same server stack the wave-2 database work puts the root Postgres container in, and through which company apps connect to their database over the Docker network rather than host ports.

- **Ownership/location:** the proxy service lives in `{server-root}/CompanyHQ/docker-compose.yml`, created and managed by CompanyHQ (root app/CLI). This wave creates the server stack with the proxy; wave 2 adds the root Postgres service to the same file.
- **Lifecycle:** the CompanyHQ server stack is started before any company stack and is never started, stopped, or torn down by company create/delete. Deleting a company removes only that company's containers and its route.
- **Networking:** one external Docker network named `companyhq`, created with the server stack. Every company app container joins it with the network alias `{slug}-app` (the Compose service name `app` collides across companies) and publishes **no** host ports. Only the shared proxy publishes 80 and 443.
- **CLI deploy/probe:** `new-company` no longer allocates per-company host ports (`SelectPort`, `appPort`/`httpsPort` go away). Readiness is probed via the app container's Docker healthcheck (`docker inspect`/`compose ps`) instead of `https://localhost:{httpsPort}`.
- **Interim routing until wave 3:** the proxy routes `{slug}.localhost` to `{slug}-app:8080`; `company.json` and the registry `adminUrl` become `https://{slug}.localhost/admin`. Wave-3 hostname routing layers customer-owned hostnames onto this same proxy.

### Scope

- [x] Replace per-company Caddy services with one server-level Caddy, Nginx, or Traefik service.
- [x] Publish only ports 80 and 443 from the shared proxy.
- [x] Generate company application containers without host port mappings.

### Task-specific tests

- [x] Generate a server deployment with two companies and inspect Compose output.
- [x] Assert exactly one proxy service publishes `80` and `443`.
- [x] Assert neither application container nor Postgres has a published host port.
- [x] Assert both applications share the internal Docker network with the proxy.

## Wave gate

- [x] The full Web test suite is green (8 pre-existing authorization/component failures must be fixed before this wave is certified complete).
