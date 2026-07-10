# Cookie strategy

Browser authentication uses a stateful session referenced by an HTTP-only cookie. Client-side code cannot read or construct the session credential.

## Production cookie contract

| Attribute | Requirement |
| --- | --- |
| Name | `__Host-companyhq-session`, or an equivalently host-prefixed environment-specific name. |
| `Secure` | Required outside an explicit local development environment. |
| `HttpOnly` | Always required. |
| `Path` | `/`. |
| `Domain` | Omitted so the cookie remains host-only. |
| `SameSite` | `Lax` by default; `Strict` where the product flow permits it. |
| Expiry | No later than the server-side session expiry. |

The value is a high-entropy opaque reference and secret verifier input. It contains no user record, role, permissions, company data, JWT claims, or readable personal data.

## Browser request flow

The browser includes the cookie only for its permitted host and path. The server hashes or otherwise safely verifies the secret portion, loads the session, checks expiry and revocation, resolves the current user and company membership, and then authorizes the operation. A valid cookie is identity proof, not authorization for every resource.

Requests made by browser code include credentials explicitly when calling the same trusted origin. Authenticated cross-origin browser calls are disabled by default.

## CSRF protection

Every unsafe cookie-authenticated operation requires:

- an exact trusted `Origin` check, with a tightly controlled `Referer` fallback only where necessary;
- a server-issued CSRF value bound to the session and submitted outside the cookie;
- an allowed content type and method;
- the normal authorization and step-up checks.

`SameSite` is defense in depth, not the only CSRF control. CORS uses exact origins and never combines credentialed requests with a wildcard origin.

## Separate frontend origins

The preferred deployment serves the browser application and API through the same site. If a project must use a separate site, it requires an explicit security profile, exact CORS and CSRF origins, `Secure`, a documented browser compatibility assessment, and security tests. A broad parent-domain cookie is forbidden because one compromised subdomain would endanger every sibling.

## Rotation and clearing

Login, refresh, privilege elevation, password change, MFA change, and recovery rotate or revoke sessions according to policy. Rotation is atomic: the old secret stops working when the new one is committed. Logout clears the cookie and revokes the server session. Device/session management can revoke one session or all sessions without waiting for cookie expiry.

## Local development

Local HTTP may omit `Secure` only for an explicitly recognized loopback development origin. Production startup fails when TLS, host-only cookie, trusted-origin, or CSRF requirements are missing. Development auto-login and fixed session secrets are forbidden in shared or production environments.
