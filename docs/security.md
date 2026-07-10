# Security baseline

CompanyHQ uses a secure-by-default authentication, field-policy, request-abuse, and agent-capability boundary. Security rules are product invariants and cannot be disabled by framework, internal-call, local-worker, or provider shortcuts.

## Authentication and sessions

The complete strategy and operation contracts are in [authentication](auth/overview.md), [operations](auth/operations.md), [cookies](auth/cookies.md), [JWT](auth/jwt.md), [token data](auth/token-data.md), [API keys](auth/api-keys.md), and [custom strategies](auth/custom-strategies.md).

- Use secure, HTTP-only session cookies with an explicit same-site policy, trusted cookie domain, TLS-only transport in production, and no browser-readable session material.
- Enforce explicit CSRF and CORS allowlists. Wildcard cross-origin access is forbidden for authenticated endpoints.
- Use stateful browser sessions with opaque cookie references. Bind sessions and client tokens to issuer, audience, company context, expiration, and revocation state. Rotate refresh credentials and detect reuse.
- Provide device/session visibility, single-session revocation, and revoke-all capability.
- Require verified email where password sign-in is allowed. Use enumeration-safe reset and verification responses, short-lived single-use hashed recovery tokens, and abuse limits.
- Apply a documented password policy and modern one-way password hashing. Passwords, recovery codes, and hashes are never returned, logged, exported, or passed to agents.
- Rate-limit and lock out repeated failed login, reset, verification, and API-key attempts by account and source.
- Require MFA for owners and administrators. Require step-up authentication for role changes, secrets, recovery, production deployment, financial actions, and other protected approvals.
- API/service keys are scoped, expiring, rotatable, revocable, stored as one-way verifiers, displayed once, and never unrestricted by default.
- Custom identity providers verify signature, issuer, audience, expiry, and tenant mapping. A provider claim authenticates an identity; CompanyHQ policy still authorizes it.

## Authorization and data

- Default deny at company, resource, record, field, and operation level.
- Enforce the same policy in UI, direct API, local/internal API, hooks, jobs, Explorer, source control, MCP, and tests.
- Do not provide a production equivalent of an access-control override. Internal execution must carry an actor and obey policy.
- Return DTO projections only. Do not serialize raw models, hidden fields, token data, secrets, internal errors, or excessive relationship depth.
- Treat a mutable slug or client-supplied identifier as lookup input, never as proof of authorization.

## Agent and MCP threat model

- An agent is delegated from a staff member and cannot exceed that staff member's current access.
- Capability issuance binds company, user, agent profile, task, Change, worktree, service/project, action, expiry, nonce, and approval. Recheck every call and revoke immediately when any scope becomes invalid.
- Use authenticated local and remote transports, TLS for remote use, replay-resistant capability tokens, rotation, revocation, and audit correlation.
- Default-deny tool, resource, and prompt discovery. Validate input schemas, paths, URLs, resource URIs, sizes, pagination, and output schemas before execution.
- Never expose generic CRUD, raw database queries, unrestricted shell, host Docker, source-control credentials, arbitrary file reads, arbitrary URLs, or production access overrides.
- Treat comments, DOM text, CSS classes, screenshots, webhooks, emails, repository files, external pages, and tool output as untrusted data, not instructions. Defend against prompt injection, path traversal, command injection, SSRF, and hostile resource URIs.
- Bind approvals to an exact action, diff, worktree, revision, and expiry to prevent approval reuse or time-of-check/time-of-use gaps.

## API, uploads, and integrations

- Version APIs and DTOs. Preserve every published major and document compatibility plus matching MCP mappings for each one.
- Authenticate custom APIs by default. Webhooks are the narrow exception: verify signatures, prevent replay, deduplicate, and enqueue work.
- Apply secure response headers and a restrictive content-security policy to admin/project surfaces; prevent unsafe framing and browser injection paths.
- Disable query systems not required by the product. If enabled, enforce request, depth, pagination, query-size, and complexity limits.
- Validate MIME type and content, scan uploads, enforce size/quota limits, quarantine suspicious files, use private or signed delivery, and preserve tenant isolation.
- Store secrets outside source. Redact them from prompts, DTOs, logs, traces, errors, screenshots, backups, and notifications.

## Supply chain and operations

- Pin, review, and scan production dependencies, container images, provider adapters, and agent runtimes. Record component/version ownership and a vulnerability response path.
- Separate development, preview, and production credentials. Development-only shortcuts, auto-login, mock credentials, and access overrides are forbidden in production.
- Centralize audit/security logs with access control, retention, alerting, and tamper-resistant records. Logs are evidence, not a second data-leak channel.

## Security gate

Security evidence is required before a Change can be approved when it alters auth, access, DTOs, agents, MCP, source control, services, uploads, integrations, or external exposure. The required negative tests are defined in [testing](testing.md).
