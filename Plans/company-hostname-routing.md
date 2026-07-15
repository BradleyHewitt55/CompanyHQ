# Company hostname routing

## Goal

Route customer-owned hostnames through the shared proxy to their matching private application container.

## Scope

- Add and remove hostname routes as companies are provisioned or deleted.
- Preserve customer ownership of DNS: never create DNS records, request Cloudflare credentials, or route traffic through CompanyHQ.
- Configure HTTPS for the customer hostname at the shared proxy.

## Task-specific tests

- Provision `acme.test` and `beta.test` on one server configuration.
- Send HTTPS requests with each hostname and assert each reaches its own application marker.
- Remove Beta and assert its hostname route is gone while Acme continues to respond.
- Assert generated configuration contains no Cloudflare credential or DNS-provider integration.
