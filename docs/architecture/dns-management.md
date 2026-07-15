# DNS management for self-hosted companies

## Decision

CompanyHQ does **not** manage customer DNS and customer subdomains do **not** point to CompanyHQ infrastructure.

Each customer owns the DNS zone and server that host their CompanyHQ installation. The customer, or their administrator, creates and maintains the DNS records at Cloudflare or their preferred DNS provider.

## Two companies on one customer server

If a customer hosts Acme and Beta on the same server, both hostnames resolve directly to that customer's server:

```text
acme.customer-domain.com --+
                           +-- customer server public IP
beta.customer-domain.com --+
```

At Cloudflare, the customer creates proxied `A`/`AAAA` records for `acme` and `beta` with the server's public IP. A wildcard record (`*.customer-domain.com`) is an optional alternative when all subdomains should resolve to that server.

## Server routing

The customer server runs one public reverse proxy (Caddy, Nginx, or Traefik) on ports 80 and 443. It routes by the incoming hostname to each company's internal CompanyHQ app container:

```text
Cloudflare or DNS-only traffic
        |
        v
customer-owned reverse proxy on :443
        |-- acme.customer-domain.com -> Acme app container
        `-- beta.customer-domain.com -> Beta app container
```

Each company keeps its own application container(s), Postgres database or database volume, and persistent data. The shared proxy is the only component that binds public HTTP/HTTPS ports.

## TLS and security

- With Cloudflare proxying enabled, use **Full (strict)** TLS and an origin certificate valid for each hostname (or a suitable wildcard certificate).
- Do not expose each CompanyHQ app's development port directly to the internet.
- Restrict the origin firewall to ports 80/443 and, when proxied, Cloudflare origin IP ranges.

## What CompanyHQ should do

CompanyHQ should accept/store the customer-selected hostname as installation configuration and generate deployment instructions. It must not require Cloudflare credentials, create DNS records, or route customer traffic through CompanyHQ-operated infrastructure.
