# Company Routing — Decisions

## Deployment model
Customers self-host on their own servers. Each company gets a **fully separate instance** — own container, own DB, own domain. No shared multi-tenant process, no `CompanyId` column, no shared DI container.

## Cross-company reporting
If cross-company visibility (e.g. sales) is ever needed, it requires a separate aggregation path since there's no shared DB to query:
- Preferred: push-based — each instance periodically pushes summary data to a central endpoint.
- Alternative: pull-based — central service polls each instance's API (requires inbound network access, more fragile).
- Not a priority feature currently.

## Reverse proxy: Caddy
Ship Caddy alongside the app in the customer's deployment. Reasons: automatic Let's Encrypt cert issuance/renewal per domain, zero manual TLS config, graceful zero-downtime reloads.

- Customer's only manual step: point their domain's DNS at their server.
- App only needs to accept the incoming host header (no hardcoded `AllowedHosts`).

## Multiple companies on one server
Supported via one Caddy instance in front of multiple app containers, routed by domain (SNI/Host header) — same pattern as OrchardCore's host-based tenant routing, but done at the infra layer instead of inside the app.

Each company gets its **own config file**, not a shared hand-edited Caddyfile:
```
# Caddyfile
import sites/*.caddy
```
- Create company → write `sites/{domain}.caddy`
- Delete company → delete `sites/{domain}.caddy`
- Never hand-edit a shared file; config is always generated, never patched in place.

### Blast radius on reload
`caddy reload` performs a graceful, atomic config swap — it does not restart the process or drop connections for domains whose config didn't change. Adding/removing one company's file and reloading does not disrupt other companies on the same server.

If stronger isolation is ever required (no shared config file at all): run one Caddy instance per company on its own internal port, with a thin TCP/SNI router (e.g. Traefik file provider) in front. Not needed today — shared Caddy with per-company files is sufficient.

## DNS requirements
Per company: a single A (or AAAA) record pointing the domain at the server's public IP. No port in DNS — ports are resolved at connection time (80/443 by default), not by DNS.

## Preventing breakage (provisioning checklist)
1. **Validate DNS before provisioning** — confirm the domain resolves to the server's actual IP before starting the container or requesting a cert. Block provisioning until it matches.
2. **Confirm 80/443 are externally reachable**, not just locally bound — firewall/security group rules can block inbound traffic even when Caddy is listening.
3. **Surface Caddy/ACME failures loudly** — don't let cert issuance fail silently; report specific errors back through tooling.
4. **Validate the generated Caddyfile before reload** — run `caddy validate` on the new config before applying it. A bad block from provisioning must never reach a live reload.
5. **Order of operations**: write config → validate → reload. Never reload unvalidated config.
6. **Deleting a company**: stop/remove its app container → delete its `sites/{domain}.caddy` file → validate → reload → optionally clean up its now-unused cert.
