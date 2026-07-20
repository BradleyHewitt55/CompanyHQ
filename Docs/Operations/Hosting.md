# Hosting

## Decision

**Caddy** fronts the self-hosted server and manages company hosting: every
company domain terminates at Caddy, which reverse-proxies to the single Kestrel
process serving all companies.

- **One Caddy, one Kestrel.** Caddy listens on 80/443 and proxies every host to
  the Admin app; no per-company processes (see [Companies](../Architecture/Companies.md)
  — no orchestrator).
- **TLS per company domain is Caddy's job.** Use [on-demand TLS][1] so a new
  company domain gets a certificate the first time it is requested — adding a
  company never means editing certificate config.
- **The app still decides the tenant.** Caddy forwards the `Host` header
  unchanged; `CompanyResolutionMiddleware` maps it via `CompanyHq:Domains:{host}`
  to the company. Adding a company domain = DNS record → server IP, plus one
  entry in that map. Caddy needs no per-company block.
- **Kestrel stays private.** Kestrel binds to localhost only; Caddy is the only
  process exposed. `/admin` and `/api` are reached exclusively through the proxy.
- **The app honors `X-Forwarded-Proto`/`X-Forwarded-Host`** (`UseForwardedHeaders`
  in `Projects/Admin/Program.cs`, first in the pipeline). Kestrel only ever sees
  plain http from the proxy; without this, every redirect and cookie the app
  issues downgrades to `http://`.
- **Dev uses `{company}.localhost:{port}`, prod uses the actual domain.**
  `MockServer/Caddyfile` proxies `alpha.localhost:8080`-style hosts to the dev
  Kestrel; `*.localhost` resolves to `127.0.0.1` in browsers with no setup. The
  routing flow (Host header → `CompanyHq:Domains` map → company) is identical —
  in production the map keys are the companies' real domains behind the proxy
  on 443.
- **The app honors `X-Forwarded-Proto`/`X-Forwarded-Host`** (`UseForwardedHeaders`
  in `Projects/Admin/Program.cs`, first in the pipeline). Kestrel only ever sees
  plain http from the proxy; without this, every redirect and cookie the app
  issues downgrades to `http://`.
- **Dev mirrors this exactly** with `MockServer/Caddyfile`: fake apex domains
  (hosts-file entries → `127.0.0.1`) served on `https:443` via Caddy's internal
  CA — the same DNS → SNI/Host → `CompanyHq:Domains` flow production uses, so
  services are tested against what production sees.
- **Run both as systemd services** on the production box (Ubuntu LTS assumed),
  Kestrel under a dedicated non-root user.

Minimal Caddyfile shape:

```caddyfile
{
    on_demand_tls {
        ask http://localhost:5000/api/domains/allowed
    }
}

https:// {
    tls {
        on_demand
    }
    reverse_proxy localhost:5000
}
```

The `ask` endpoint (not yet built) lets the app confirm a domain belongs to a
registered company before Caddy issues a certificate — required to stop
strangers minting certs against the server.

[1]: https://caddyserver.com/docs/automatic-https#on-demand-tls
