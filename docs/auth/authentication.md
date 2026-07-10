# Authentication

> **Document status:** Target V1 security contract.

V1 supports local email/password accounts with Argon2id password hashing, email verification, recovery tokens, MFA for protected roles/actions, and server-side sessions. Sessions use opaque random IDs stored as `Secure`, `HttpOnly`, `SameSite=Lax` cookies; state-changing requests require origin/CSRF protection. Session records include installation, user, authentication methods/strength, issued/last-seen/absolute expiry, device metadata, and revocation.

API integrations use scoped, hashed credentials with rotation and last-used audit. Agent/MCP access uses short-lived signed capabilities derived from an authenticated delegator or approved service identity; it never reuses browser cookies.

Login, recovery, invitation, MFA enrollment, role change, credential creation, and session revocation are rate-limited and audited. Enumeration-safe responses and token hashing are mandatory.
