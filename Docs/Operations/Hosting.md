# Hosting

## Decision

**Caddy** fronts the self-hosted server and manages company hosting: every
company domain terminates at Caddy, which reverse-proxies to **that company's own
Admin process**. One process serves exactly one company; isolation is enforced by
process separation, not by an in-app tenant check.

- **One Caddy, one process per company.** Caddy listens on 80/443 and proxies each
  company's host to that company's Admin process. Each process is bound at startup
  by `CompanyHq:Company` and only ever loads its own services plus the shared
  built-ins — a rebuild in one company cannot reach another (see
  [Companies](../Architecture/Companies.md)).
- **TLS per company domain is Caddy's job.** Use [on-demand TLS][1] so a new
  company domain gets a certificate the first time it is requested — adding a
  company never means editing certificate config.
- **Routing maps domain → process, not header → tenant.** Caddy points each
  company domain at its process's port. Adding a company = DNS record → server IP,
  a new `admin-{company}` process, and one Caddy block. The app never reads the
  `Host` header to pick a tenant; every scoped `ICompanyContext` starts with the
  process's fixed `CompanyHq:Company`.
- **Kestrel stays private.** Each Admin process binds to localhost/an internal
  port only; Caddy is the only process exposed. `/admin` and `/api` are reached
  exclusively through the proxy.
- **The app honors `X-Forwarded-Proto`/`X-Forwarded-Host`** (`UseForwardedHeaders`
  in `Projects/Admin/Program.cs`, first in the pipeline). Kestrel only ever sees
  plain http from the proxy; without this, every redirect and cookie the app
  issues downgrades to `http://`.
- **Dev uses `{company}.localhost:{port}`, prod uses the actual domain.**
  `docker-compose.yml` runs an `admin-{company}` service per company and
  `MockServer/Caddyfile` proxies `alpha.localhost:8080` → `admin-alpha`,
  `beta.localhost:8080` → `admin-beta`; `*.localhost` resolves to `127.0.0.1` in
  browsers with no setup. The routing flow (domain → the matching process) is
  identical to production, where the Caddy blocks key on the companies' real
  domains behind the proxy on 443.
- **Rebuild to activate service changes.** `Settings/service.json` is copied beside
  image-baked service binaries. Set `enabled`, then run
  `docker compose build admin-{company}` and recreate that Admin service.
- **Keep binaries and state separate.** `CompanyHq:PluginRoot` is immutable image
  content. `CompanyHq:ServerRoot` is mounted state containing service source,
  `db.json`, and generated files; it never shadows the scanner's plugin path.
- **One Postgres server, isolated company databases.** `CompanyHqServer` is the
  provisioning administrator connection. Choosing local storage creates a dedicated
  database and role and writes its generated connection to the company's `db.json`;
  choosing external storage writes the supplied connection instead. Compose stores
  managed credentials with host `localhost` for host-side CLI use and applies
  `CompanyHq:ManagedDatabaseHost=postgres` inside Admin containers. External
  connection strings are never rewritten. Persist both Postgres data and the
  company directory.
- **Run each company's process and Caddy as services** on the production box
  (Ubuntu LTS assumed, or the compose stack), each Admin process under a dedicated
  non-root user.

Minimal Caddyfile shape (one block per company, each pointing at that company's
process):

```caddyfile
{
    on_demand_tls {
        ask http://localhost:5000/api/domains/allowed
    }
}

acme.example.com {
    tls {
        on_demand
    }
    reverse_proxy localhost:5001   # admin-acme
}

globex.example.com {
    tls {
        on_demand
    }
    reverse_proxy localhost:5002   # admin-globex
}
```

The `ask` endpoint (not yet built) lets the app confirm a domain belongs to a
registered company before Caddy issues a certificate — required to stop
strangers minting certs against the server.

[1]: https://caddyserver.com/docs/automatic-https#on-demand-tls
