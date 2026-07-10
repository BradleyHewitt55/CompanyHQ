# Authentication and identity

Authentication establishes who is acting. Authorization evaluates what that identity may do for the current company, operation, record, field, service, project, and approval state. Both checks run on every entry point, including internal calls, jobs, hooks, agents, and MCP.

## Identity types

| Identity | Authentication | Authority source |
| --- | --- | --- |
| Platform owner | Verified human account, MFA, and step-up for protected work. | Explicit platform and company assignments. |
| Company staff member | Verified human account with a revocable browser or client session. | Company membership, roles, and record/field policy. |
| Customer | Project-specific verified account. | The owning project's customer policy. |
| Service identity | API key or approved workload credential. | Narrow service/project scopes and operations. |
| Delegated agent | Short-lived capability issued for one authenticated delegation. | Intersection of the staff principal, profile, role, task, service/project, Change, worktree, and approval. |

An agent never receives a person's password, browser cookie, recovery credential, MFA secret, or personal source-control credential.

## Default strategy

Browser sign-in uses a stateful server session and a secure, HTTP-only, host-only cookie. The cookie contains an opaque session reference, not a raw user record, permissions, or a long-lived bearer credential. See [cookies](cookies.md).

JWT bearer access is available for approved non-browser clients. API keys are for named service identities. Custom strategies can establish an external identity, but every strategy ends at the same local authorization and DTO projection boundary. See [JWT](jwt.md), [API keys](api-keys.md), and [custom strategies](custom-strategies.md).

## Local password strategy

Company staff sign in with a normalized verified email address by default. A project may explicitly support a unique username, with configuration deciding whether email remains an accepted identifier and whether an email address is still required. Public responses never reveal which identifier matched.

The local strategy can be disabled only when at least one tested replacement strategy and an owner recovery path exist. Disabling password sign-in does not delete password history, sessions, external links, or audit evidence without a separate retention decision.

Account state is explicit:

~~~text
invited -> pending verification -> active
active -> locked or suspended -> active after authorized recovery
active or suspended -> archived
~~~

Locked is an abuse-control state with bounded expiry or administrative unlock. Suspended and archived are policy states and are never cleared merely by successful credential verification.

## Sign-in flow

~~~text
receive credentials -> normalize identifier -> abuse and lock checks
-> verify password or external proof -> require verified account
-> complete MFA when required -> create or rotate session
-> set secure cookie or return approved client credential
-> project authenticated-user DTO -> audit outcome
~~~

Failures use enumeration-safe responses. Rate limits cover account, source, company, and device signals. Successful authentication never skips authorization for the requested operation.

## Session lifecycle

Sessions have an identifier, user, company context, creation and activity times, absolute and idle expiry, authentication methods, device label, token version, and revocation state. Sensitive session secrets are stored only as one-way verifiers.

Refresh rotates the session secret. Reuse of an already rotated secret revokes the session family and emits a security event. Logout revokes the current session; logout-all increments the user's token version and revokes every active session. Role, company, password, MFA, recovery, suspension, or security-sensitive identity changes can revoke sessions immediately.

## Password and MFA baseline

- Passwords use a modern memory-hard one-way hash with a unique salt and centrally managed parameters.
- Password policy rejects known-compromised and context-derived passwords, supports long passphrases, and never silently truncates.
- Owners and administrators require MFA. Step-up authentication is required for recovery, role/policy changes, secrets, provider enablement, production deployment, financial actions, and destructive operations.
- TOTP and platform authenticator methods are preferred. Recovery codes are single-use, stored as one-way verifiers, shown once, and regenerate only after step-up.
- Failed password and MFA attempts are throttled and audited without recording secrets.

## Verification and recovery

Email verification and password recovery use separate, random, single-use, short-lived credentials stored as one-way verifiers. Starting either flow returns the same public response whether the account exists or not. Completion validates purpose, account, company/project, expiry, use state, and request limits before changing anything.

Password reset revokes existing sessions and service credentials according to policy. Verification does not sign a user in unless the configured flow explicitly requires and records that behavior. See [authentication email](email.md) and [operations](operations.md).

## Returned identity data

Authentication middleware resolves an internal principal, but responses expose only the operation's policy-projected identity DTO. Password hashes, session secrets, MFA secrets, recovery values, API-key verifiers, internal strategy data, hidden user fields, and raw models are never returned, logged, exported, or supplied to agents.

## Configuration contract

The authentication service declares:

| Setting | Contract |
| --- | --- |
| Identifier mode | Email by default; username or either only when uniqueness, normalization, verification, and recovery are defined. |
| Verification | Required for password sign-in unless a project-specific policy explicitly supplies equivalent verified identity. |
| Login limits | Maximum attempts, observation window, lock duration, source/account limits, and safe unlock policy. |
| Session limits | Idle and absolute lifetime, refresh rotation, concurrent-session policy, and revoke behavior. |
| Cookie and origins | Host-only cookie settings, exact trusted origins, CSRF, and credentialed CORS rules. |
| Client tokens | Allowed audiences, access lifetime, signing keys, rotation overlap, and revocation checks. |
| MFA and step-up | Required roles/actions, allowed methods, freshness window, and recovery behavior. |
| Strategies | Explicitly enabled password, cookie, JWT, API-key, and custom strategies with order and scope. |
| Messages | Approved verification/recovery origins, templates, expiry, sender, and abuse controls. |
| Audit and retention | Safe events, security alerts, session/key history, and deletion rules. |

Insecure development shortcuts, auto-login, fixed credentials, disabled origin checks, and verbose credential errors are rejected in production.

Related documents: [operations](operations.md), [cookies](cookies.md), [JWT](jwt.md), [token data](token-data.md), [API keys](api-keys.md), [custom strategies](custom-strategies.md), [access control](access-control.md), and [security](../security.md).
