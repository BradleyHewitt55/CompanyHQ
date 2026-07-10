# API-key strategy

API keys authenticate named service identities for integrations and automation. They are not human passwords, browser credentials, or a shortcut around CompanyHQ policy.

## Key record

Each key records a public identifier and prefix, one-way verifier, service identity, company, optional project, allowed API majors, operations/scopes, creation and expiry times, creator, last-used summary, rotation family, status, and audit references.

The secret is generated with cryptographically secure randomness and shown once. CompanyHQ stores only a one-way verifier. Losing the value requires rotation; support staff cannot retrieve it.

## Request format

~~~text
Authorization: CompanyHQ-Key <public-id>.<secret>
~~~

The server uses the public identifier to find a bounded candidate record, verifies the secret in constant-time-compatible logic, checks expiry, status, company, environment, source restrictions when configured, API major, and scope, and then runs normal authorization and DTO projection.

## Lifecycle

Creation requires an authenticated user with key-management permission and step-up when policy requires it. Rotation creates an overlapping replacement with a short cutover window, records which consumers must move, and then revokes the previous key. Revocation is immediate. Expired and revoked keys fail with a generic authentication error.

Keys default to an expiry and the narrowest service operations. Non-expiring keys require an explicit owner policy and are still rotatable, revocable, tenant-bound, monitored, and never unrestricted.

## Restrictions

- Keys never identify a human user or delegated agent.
- A key cannot grant scopes its service identity does not already have.
- Keys are forbidden in browser code, URLs, query strings, source, repository settings, command history, logs, screenshots, notifications, and agent context.
- Different environments and companies use different keys.
- Failed verification and anomalous use are rate-limited and audited without recording the secret.

## Administration

The Keys page lists safe metadata only: name, prefix, service, scopes, creator, creation/expiry, last-used summary, and status. Creating, rotating, narrowing, or revoking a key produces an audit event. Expanding scope is a new protected authorization decision, not a metadata edit.
