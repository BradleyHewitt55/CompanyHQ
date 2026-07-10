# Authentication operations

> **Document status:** Target V1 security contract.

The authentication service exposes a consistent operation set to the admin workspace and approved projects. Routes below use `v1`; every published major remains supported under the [API-version contract](../architecture/api-versioning.md).

## Operation registry

| Operation | Route | Public result and security behavior |
| --- | --- | --- |
| Sign in | `POST /api/v1/auth/login` | Creates a session only after credential, account, abuse, verification, and MFA checks. |
| Sign out | `POST /api/v1/auth/logout` | Revokes the current session and clears its cookie. |
| Sign out all | `POST /api/v1/auth/logout-all` | Requires recent authentication; revokes every session for the principal. |
| Refresh | `POST /api/v1/auth/refresh` | Rotates the current session secret or approved refresh credential and detects reuse. |
| Current identity | `GET /api/v1/auth/me` | Returns the current policy-projected identity DTO and session expiry, never credential material. |
| Access summary | `GET /api/v1/auth/access` | Returns UI hints for allowed areas and operations; handlers still authorize independently. |
| List sessions | `GET /api/v1/auth/sessions` | Returns the user's safe device/session DTOs. |
| Revoke session | `DELETE /api/v1/auth/sessions/(session-id)` | Revokes an owned session; administrative revocation requires explicit policy. |
| Start verification | `POST /api/v1/auth/verification/start` | Sends a verification message with an enumeration-safe response. |
| Complete verification | `POST /api/v1/auth/verification/complete` | Consumes one verification credential for its exact purpose and account. |
| Start recovery | `POST /api/v1/auth/password/forgot` | Sends recovery instructions with an enumeration-safe response. |
| Complete recovery | `POST /api/v1/auth/password/reset` | Consumes one recovery credential, changes the password, and revokes affected sessions. |
| Unlock account | `POST /api/v1/auth/accounts/(account-id)/unlock` | Administrative, audited, step-up-protected operation; never a public self-service bypass. |
| Begin MFA | `POST /api/v1/auth/mfa/challenge` | Starts a method-specific challenge for a partially authenticated transaction. |
| Complete MFA | `POST /api/v1/auth/mfa/verify` | Completes the bound transaction; cannot be replayed for another sign-in or approval. |
| Enrol MFA | `POST /api/v1/auth/mfa/methods` | Requires a current session and recent authentication. |
| Remove MFA | `DELETE /api/v1/auth/mfa/methods/(method-id)` | Requires step-up and preserves at least one required method or a governed recovery path. |

## Common request flow

Every operation performs transport and size validation, resolves the company/project context, applies rate and abuse controls, authenticates when required, evaluates authorization and step-up state, executes atomically, projects a response DTO, updates session or cookie state, and records an audit event.

Authentication errors use stable public codes such as `invalid_credentials`, `verification_required`, `mfa_required`, `session_expired`, `rate_limited`, and `not_authorized`. Public messages do not reveal whether an email, username, session, API key, or external subject exists.

## Login transaction

Login accepts a normalized identifier, secret proof, requested company/project context, and optional device label. The server never accepts a client-supplied role, user identifier, company membership, permission list, redirect origin, or token claims as authority.

When MFA is required, the first step returns a short-lived transaction identifier restricted to MFA completion. It is not an authenticated session and cannot access other APIs. Completing MFA atomically consumes the transaction and creates the session.

## Refresh and logout

Refresh requires an unexpired, unrevoked session family and rotates its secret in one transaction. Presenting a superseded secret is treated as possible theft and revokes the family. Concurrent legitimate refreshes receive a bounded grace policy only if the implementation can prove they belong to the same rotation transaction.

Logout is server-side revocation. Clearing a browser cookie alone is not logout. Responses that clear cookies use the same name, path, host scope, security attributes, and partition settings as the original cookie.

## Verification and recovery

Start operations always return the same status for existing, missing, disabled, or already verified accounts. Delivery occurs through a durable job. Complete operations accept a random one-time value, hash it, find a matching unused verifier, validate purpose and expiry, consume it atomically, and then apply the requested change.

Recovery values and verification values are not interchangeable. Neither value appears in logs, analytics, agent context, referrers, or notification previews.

## Access summary is not authorization

The access operation helps the UI render available navigation and actions. It contains only the current caller's safe permission summary. A stale or manipulated access response cannot authorize an operation because every target handler evaluates policy again.
