# JWT strategy

JWT bearer access is an explicit strategy for approved non-browser clients and internal service boundaries that require signed, time-limited claims. Browser sessions use the [cookie strategy](cookies.md) by default.

## Issuance

A JWT is issued only after the same credential, verification, MFA, company, abuse, and account-state checks used by session login. The issuer uses an asymmetric signing key from the secret store, publishes only verification keys, includes a key identifier, and supports overlapping key rotation.

Access tokens are short-lived. Refresh credentials are separate rotating secrets backed by server-side session state; a long-lived self-refreshing JWT is forbidden.

## Validation order

Every recipient validates the allowed algorithm, key identifier, signature, issuer, audience, subject, company/project context, not-before time, expiry, unique token identifier, session identifier, token version, and revocation state before policy evaluation. Algorithm negotiation from untrusted token input is forbidden.

Clock skew is small and explicit. Missing or unknown claims fail closed. A token valid for one audience, company, project, environment, or service is invalid everywhere else.

## Authorization header

Approved clients send:

~~~text
Authorization: Bearer <access-token>
~~~

Tokens must not appear in URLs, query strings, analytics, logs, error details, source files, browser storage, screenshots, or agent prompts. Transport requires TLS.

## Response behavior

Browser login and refresh responses do not return bearer tokens. Non-browser clients receive tokens only through a strategy-specific response DTO and only when their client registration allows it. `me` returns an identity DTO and expiry metadata, never the raw bearer value.

## Key rotation and revocation

Signing keys have identifiers, activation windows, rotation ownership, audit records, and emergency revocation procedures. Verification accepts the current and explicitly overlapping prior keys only. User token-version changes, session revocation, password recovery, account suspension, company removal, or key compromise invalidate affected tokens before their nominal expiry.
