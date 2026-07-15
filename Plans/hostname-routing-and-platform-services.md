# Company hostname routing and the platform services

Wave 3. Four parallel lanes; each agent owns exactly one task section below.

## Task: Company hostname routing

### Goal

Route customer-owned hostnames through the shared proxy to their matching private application container. Needs the wave-1 proxy and wave-2 provisioning changes settled (both edit the company-provisioning flow).

### Scope

- [ ] Add and remove hostname routes as companies are provisioned or deleted.
- [ ] Preserve customer ownership of DNS: never create DNS records, request Cloudflare credentials, or route traffic through CompanyHQ.
- [ ] Configure HTTPS for the customer hostname at the shared proxy.

### Task-specific tests

- [ ] Provision `acme.test` and `beta.test` on one server configuration.
- [ ] Send HTTPS requests with each hostname and assert each reaches its own application marker.
- [ ] Remove Beta and assert its hostname route is gone while Acme continues to respond.
- [ ] Assert generated configuration contains no Cloudflare credential or DNS-provider integration.

## Task: Resend email service

### Goal

Provide a toggleable platform email service with encrypted key storage, an admin settings page, and authenticated send API. Needs platform tagging, settings pages, and `BaseModel` entities from wave 2.

### Scope

- [ ] Add the Resend platform service, manifest, NuGet dependency, convention tree, and documentation.
- [ ] Persist Resend settings and encrypted API keys as parent/child `BaseModel` entities.
- [ ] Implement `IEmailService` with idempotency and typed handling for 401, 403, and 429 responses.
- [ ] Provide a masked settings page and a ManageUsers-protected, antiforgery-protected send endpoint.

### Task-specific tests

- [ ] Verify encrypted key persistence and decrypt round-trip through entity services.
- [ ] Fake the HTTP handler and test success, 401, 403, 429 retry, and mandatory idempotency keys.
- [ ] Assert endpoint outcomes: anonymous 401, Viewer 403, Admin success, and missing antiforgery rejection.
- [ ] bUnit-test that the settings page masks keys and never renders plaintext.

## Task: Cloudflare R2 storage service

### Goal

Provide a toggleable platform object-storage service backed by Cloudflare R2 with encrypted credentials and an admin settings page. Same prerequisites as the Resend service; disjoint service directory.

### Scope

- [ ] Add the CloudflareR2 platform service, manifest, AWS S3 dependency, convention tree, and documentation.
- [ ] Persist R2 settings and encrypted secrets as parent/child `BaseModel` entities.
- [ ] Implement `IObjectStorageService` for put, get, delete, list, and presign.
- [ ] Configure the R2 endpoint, `auto` region, path-style access, required-only checksums, safe key validation, and truncated-list pagination.

### Task-specific tests

- [ ] Assert S3 client configuration uses the account endpoint, `auto` region, path style, and required-only checksum settings.
- [ ] Verify encrypted secret persistence and ensure the settings page never echoes plaintext.
- [ ] Test put/get/delete/list pagination against a fake S3 client.
- [ ] Assert traversal and leading-slash object keys are rejected.
- [ ] Add an optional env-gated live R2 test marked `External`.

## Task: MCP API proxy service

### Goal

Expose each company's existing `/api/*` endpoints as an always-on, company-isolated MCP server. Needs platform tagging; hidden service, no settings page.

### Scope

- [ ] Add the hidden, platform-tagged MCP service with the official ASP.NET Core MCP SDK and documentation.
- [ ] Serve `/mcp` using bearer-token authentication; no configured token returns 404.
- [ ] Generate MCP tools from active API endpoints and execute them through loopback HTTP so existing authorization and service gating apply.
- [ ] Generate a per-company MCP token in protected scaffold configuration.
- [ ] Document the Owner-scoped automation principal explicitly.

### Task-specific tests

- [ ] Assert missing and invalid tokens return 401 and an unconfigured token returns 404.
- [ ] Use an MCP SDK client to complete initialization and verify known API tools appear in `tools/list`.
- [ ] Invoke a tool for a disabled service endpoint and assert the proxied 404 is preserved.
- [ ] Runtime-load a service and assert tool inventory refreshes, or test and document the explicit refresh trigger.
- [ ] Scaffold two companies and assert each MCP endpoint rejects the other company's token.
