# Company website container hosting

## Goal

Host each company website in its own Docker container — one container per company, never per page — so a website can be restarted, rebuilt, and isolated without touching the server or other companies.

## Scope

### Container model

- Create one website container per company; the container is the unit of restart.
- Website source lives at `Companies/{company-slug}/Projects/Website/`.
- Containers join the shared proxy network with no published host ports; the shared reverse proxy routes hostname → container (see `shared-proxy-topology.md`).
- Two modes:
  - **Edit mode:** mount the company workspace as a volume into a shared runtime image running `dotnet watch`; page edits apply without an image rebuild.
  - **Publish mode:** `dotnet publish` and bake a company-specific image when the user publishes.
- Control Docker through Docker.DotNet, exposing only: create, start, stop, restart, rebuild, logs, status. No arbitrary Docker operations from the UI.

### Explorer integration

- Add a "+" (add project) button to the admin file explorer that scaffolds a website project for the company and creates its container.
- The explorer manages the website's `Pages/`: adding a page creates the trio `{Page}.razor` (markup), `{Page}.razor.css` (scoped styles), `{Page}.razor.cs` (code-behind partial class for component logic).
- Services stay out of `.razor.cs`; they live under `Services/{Service}/Services` and are injected via DI.
- The explorer surfaces container controls per website: restart, rebuild, logs, status.

### MockServer

- Add a `MockServer/` directory at the project level that reproduces the production topology on local Docker: shared reverse proxy, shared Postgres, and company website containers.
- Local dev tests run against MockServer so the prod flow (create company → add website → route through proxy) is exercised locally.
- MockServer state is disposable; tearing it down removes all containers, networks, and volumes it created.

## Task-specific tests

- From the explorer, click "+" and add a website to `Acme` — assert the scaffold exists at `Companies/acme/Projects/Website/` and a container is created on the shared network with no published host port.
- Add a page from the explorer — assert the `.razor`/`.razor.css`/`.razor.cs` trio is created and an edit to the page is served without an image rebuild.
- Restart Acme's website from the explorer — assert the container restarts and the site returns healthy through the proxy.
- With `Acme` and `Beta` both running, restart Acme's container — assert Beta's site stays up and unaffected.
- Assert the Docker control surface rejects any operation outside create/start/stop/restart/rebuild/logs/status.
- Spin up MockServer on local Docker, run the full flow (create company, add website, add page, publish, request the site through the proxy), then tear it down — assert no MockServer containers, networks, or volumes remain.
