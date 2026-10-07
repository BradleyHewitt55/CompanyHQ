# Auth service

`Services/Auth` (`CompanyHQ.Services.Auth`) issues and validates the tokens every other service
trusts, and owns every entity that makes a credential revocable or single-use: `RefreshToken` and
`AuthToken`.

It owns **no account data** — `Services/User` owns the `User` record, the password hasher, and the
deletion path — so it reads users through `PostgresDbContext`, never through a `User`-shaped API of
its own, but it **does** write two columns on that row: `EmailVerified` and `SessionsRevokedAt` (see
`Services/User/AGENTS.md` for which flow stamps which). It decides **no
access rule**: it registers one authorization policy per name in `AccessPermissions.All`, plus
`AuthPolicies.RequireVerifiedEmail`, configures the bearer scheme, hands other controllers
`[Authorize]` / `User.GetUserId()` / `User.HasPermission()`, and stays out of the way otherwise.
Its mail goes through its own `IAuthEmailSender` port.

## Two tokens, on purpose

- **Access token** — HS256 JWT, 15 min, `Authorization: Bearer`. The signature is verified with no
  lookup, but `OnTokenValidated` then reads the account row and **replaces the `role`, `email`,
  `given_name`, `family_name`, and `email_verified` claims from the database on every request**, and fails the
  token outright when its `iat` predates `User.SessionsRevokedAt`. A deleted account fails validation
  outright; a promotion, demotion, or panic revocation takes effect on the next request rather than
  the next token. The token is therefore never the authority on what a caller may do, and — for the
  flows that stamp `SessionsRevokedAt` — it is no longer unrevocable either. Flows that revoke a
  session **without** stamping it (`DELETE /sessions/{id}`) still leave that access token's 15-minute
  window as the blast radius, on purpose: ending one device must not silently kill another's live
  requests mid-flight.
- **Refresh token** — 256 random bits, Base64Url, 30 days, one `refresh_token` row, carries an
  optional sanitized `DeviceLabel`. Revoke the row and the session ends at the next renewal.

`TokenHash` is an *unsalted* hex SHA-256 digest on both `RefreshToken` and `AuthToken`, and that is
correct here: the row is found *by* it and a 256-bit random token has no guessable input space.
**Never reason that way about a password.**

**Refreshing rotates** — the presented token is revoked and the new one inserted in a single
`SaveChangesAsync`. A revoked row is **kept, never deleted**, which is what makes reuse detection
possible (see Traps). `RefreshToken` and `AuthToken` both have no navigation property to `User`; the
FK is `HasOne<User>()` with cascade delete, so deleting an account kills every session and every
outstanding link.

## `AuthToken` — one table, four purposes

Password reset, email verification, email change, and account deletion are the same mechanism: a
single-use, short-lived, high-entropy secret mailed to an address. `AuthTokenPurpose` discriminates
one table instead of near-identical ones. `IssueAsync` **consumes every other open token of the
same purpose for that user before minting the new one** — one live link per purpose, always the
newest. `ConsumeAsync` stamps `ConsumedAt` rather than deleting the row, so a second click on the same
link reads as "already used" (400 `InvalidToken`), not a mystery 404. `PendingEmail` (email-change)
is the one payload field a purpose carries forward.

**Deleting your own account is two steps and there is no one-step route.**
`POST /api/auth/delete-account` takes the account holder's full name, typed out, and refuses anything
else (an account with no name confirms with its address). It deletes nothing: it mails a link.
`POST /api/auth/confirm-account-deletion` redeems that link, anonymously, and only then runs
`IAccountDeletionCoordinator`. The token is looked up with `FindActiveAsync`, not consumed: the row
goes with the account through its cascading key, and when a deletion guard refuses (409) the same
link still works once the holder has resolved it. Every row with a cascading key to the account
goes too, sessions included. The page the link opens must redeem it from the browser, never while
rendering: a mail scanner that only fetches the link must not delete the account.

Lifetimes: password reset 1 hour, email verification 24 hours, email change 1 hour, account
deletion 1 hour — a live
credential to a live account gets the short fuse. `Auth:WebBaseAddress` (`Auth__WebBaseAddress`
in the environment) is the origin every token-carrying email links to, always `Apps/Web`, never
a mobile deep link, so the link works in whatever mail client the person is standing in.

## Why `AuthUserDto` has no admin shape

Deliberate exception to the `{Role}{Class}Dto` rule. **The DTO describes the caller to themselves.**
There is no privileged view of your own session — an `AdminAuthUserDto` would either duplicate
`AuthUserDto` or leak operator fields to the account holder because they happen to be an operator.
`AuthSessionSummaryDto` (the `GET /sessions` row shape) inherits the same reasoning: there is no
privileged view of your own sessions either.

Same reason `AuthUserDto` carries `Email` while `ClientUserDto` omits it: hiding a caller's own
address from them protects nobody; hiding it in `/api/users` stops harvesting. Different questions,
not conflicting rules.

Consequences: no `ResponseAudience` parameter belongs on `IAuthService` — nothing here varies by
role. `AuthSessionDto` is manufactured per request, not projected, so there is nothing for EF Core
to widen. `GetUserAsync` projects straight to `AuthUserDto` in SQL; `PasswordHash` is never selected.

## Endpoints — only where the auth requirement is the point

| Route | Auth | Note |
| --- | --- | --- |
| `POST /api/auth/register` | anonymous | Always `UserRole.Client`. Distributed-rate-limited by IP and normalized email (10 per IP and 3 per email per 15 minutes). Issues and mails an `EmailVerification` token before returning the session; a send failure is logged, never surfaced. A blocked request uses the generic 429 Problem Details response. |
| `POST /api/auth/login` | anonymous | 401 on bad password **and** unknown email. Rate-limited by IP **and** normalized identity, independently. **Rehashes a stale password hash in place** when the hasher's iteration count moves on. Records a sanitized `DeviceLabel` when the request carries one. |
| `POST /api/auth/refresh` | anonymous | The refresh token in the body *is* the credential. Detects reuse — see Traps. |
| `POST /api/auth/logout` | anonymous | Always 204. Whether the token existed is not the caller's business. |
| `POST /api/auth/verify-email` | anonymous | 204/400. The link is clicked in a session-less browser; the token is the credential. |
| `POST /api/auth/resend-verification` | anonymous | **204 always**, rate-limited 3/15min per identity. Same enumeration rules as `forgot-password`. |
| `POST /api/auth/forgot-password` | anonymous | **204 always**, rate-limited 3/15min per identity + 20/15min per IP. |
| `POST /api/auth/reset-password` | anonymous | 200 `AuthSessionDto`/400. Revokes every session, stamps `SessionsRevokedAt`, invalidates every other open `PasswordReset`/`EmailChange` token, mails the notice, and signs the caller back in — proving control of the mailbox already beat a login form. |
| `POST /api/auth/change-password` | bearer | 200 `AuthSessionDto`/400/401. Requires `CurrentPassword` and the caller's own `RefreshToken` in the body (an access token alone carries no link back to a specific session) so it can rotate that one session while revoking every other. Rejects a new password equal to the current one (`WeakPassword`). Rate-limited 5/15min per user id. |
| `POST /api/auth/change-email` | bearer | 204/401/409. Requires `CurrentPassword`. Mails the confirmation to the **new** address and a notice to the **old** one; does not move `User.Email`. Rate-limited 5/15min per user id. |
| `POST /api/auth/confirm-email-change` | anonymous | 204/400/409. Moves `User.Email`, sets `EmailVerified = true` (proving the new address is exactly what verification proves), revokes every session, stamps `SessionsRevokedAt`. |
| `GET /api/auth/sessions?page=1&pageSize=10` | bearer | Active sessions only, newest first, in `PagedResult<AuthSessionSummaryDto>`. `?currentSessionId=` (from `AuthSessionDto.SessionId`) flags the caller's own row — never a refresh token, sent for that purpose. |
| `DELETE /api/auth/sessions/{id}` | bearer | 204/404. 404 when not the caller's own — never confirms another user's session id exists. Does **not** stamp `SessionsRevokedAt`. |
| `POST /api/auth/sessions/revoke-all` | bearer | 204. Sign out everywhere, including the caller's own live access token — stamps `SessionsRevokedAt`. |
| `GET /api/auth/me` | bearer | Read from the database, not the token's claims, so a renamed, re-roled, or newly-verified account is visible before the token expires. Includes the role name and its permissions. |
| `DELETE /api/auth/me` | bearer | Routes through the User service's `AccountDeletionCoordinator`, the same path `DELETE /api/users/{id}` uses, so neither can skip a guard. Its **409 is a generic guard veto**. |

**There is exactly one place a `User` row is constructed and its password hashed**
(`AuthService.CreateUserAsync`). Keep it that way: a second copy is how
two sign-up paths silently drift on role, trimming, or hasher.

Emails are normalised to trimmed lower-case on register, login, forgot-password, resend-verification,
and change-email. That, not a collation, is what makes the unique index case-insensitive.

## Traps

- **`NameClaimType` is `AuthClaims.Subject`, so `User.Identity.Name` is the user id.** Read ids
  through `GetUserId()`, never `Identity.Name`.
- **`MapInboundClaims` is off.** Claims keep their short names (`sub`, `role`, `permission`);
  reaching for `ClaimTypes.*` fails because unmapped tokens carry no WS-Federation URIs.
- **Order matters twice.** `AddAuth` after `AddUsers` and `AddEmail` (password hasher, deletion
  coordinator, and `IEmailTemplateService`), and `UseAuthentication` before `UseAuthorization` or every
  `[Authorize]` endpoint sees an anonymous caller.
- `AuthService` injects `PostgresDbContext` rather than `IPostgresService<T>` — rotation, in-place
  rehash, and every mass-revocation flow are multi-row writes, and two repository calls are two
  transactions. Every write that issues a session funnels through `IssueSession`, which commits with
  whatever the caller already staged.
- **Refresh-token reuse detection excludes tokens that died in a deliberate mass revocation.**
  `RefreshAsync` revokes every other active session when a *revoked* token is replayed — a rotated
  token being reused means theft or a client racing itself, and both get "everyone signs in again."
  But a token revoked by `reset-password`/`change-password`/`confirm-email-change`/`revoke-all`
  already gave the account that moment; without the exclusion, a second device's routine next refresh
  with its now-stale token would cascade and kill the session the person is actively using immediately
  after they reset their own password. The carve-out compares the token's `RevokedAt` against the
  owner's `SessionsRevokedAt` — a token revoked at or before that stamp is treated as expected
  staleness, not a signal.
- **The `Concurrent_refresh_of_one_token_has_one_winner` shape now means *nobody* wins.** Two
  simultaneous refreshes of the same token still have one database-level winner (`FOR UPDATE`), but
  the loser's request looks exactly like a stolen-token replay to the reuse detector, so it revokes
  the winner's freshly-rotated session too. This is intentional — see the plan's own "do not try to
  distinguish a race from theft" instruction — not a regression to fix.
- **`DeviceLabel` is attacker-controlled input rendered back on `GET /sessions`.** It is sanitized on
  write (trim, strip control characters, truncate to 64 *after* stripping, empty stored as `null`) in
  `AuthService.SanitizeDeviceLabel`. Never render it as `MarkupString`; a missing label is "Unknown
  device" at the display layer, not a placeholder written into the row.

## Configuration

Section `Jwt` (`Jwt__SigningKey` in the environment), so the signing key lives in `.env` and never
`appsettings.json`.
`SigningKey` is required and must be ≥ 32 characters. Section `Auth` for `AuthTokenOptions`
(`WebBaseAddress`, required and validated as an absolute URL; the three token lifetimes, defaulted and
rarely worth overriding). A missing, short, malformed, or
non-positive-lifetime config **throws during `AddAuth`**, not on the first request where it would be
a 500. Rotating the JWT key invalidates every access token instantly; refresh tokens survive and
clients recover on renewal.

Every lifetime is computed against the injected `TimeProvider`, so a test substitutes a fake clock
instead of sleeping for however many minutes or hours the flow under test needs.

**A real `.env` value silently wins over a test's `ConfigureAppConfiguration` in-memory override for
any eagerly-validated option.** `AddAuth`, `AddResend`, and friends validate their options *before*
`WebApplicationFactory`'s `ConfigureWebHost` overrides are spliced in — the same class of "startup
throws before the test harness gets a say" that made `Resend:ApiKey`'s in-memory override in
`ApiFactory` dead code too (the real `Resend__ApiKey` in `.env` satisfies it instead). `Auth:WebBaseAddress`
hit exactly this; the fix was a real `Auth__WebBaseAddress` value in `.env` and `.env.example`, not a
harness workaround. Any future required-at-startup option needs the same treatment.

## Real-time hubs read the token from the query string

Browsers cannot set an `Authorization` header on a WebSocket or server-sent-events request, so the
bearer scheme's `OnMessageReceived` also accepts `access_token` from the query string — **only** under
`AuthHubs.PathPrefix` (`/hubs`). On an API route a query-string token is ignored (401), so it never
lands in an access log. A service mounting a hub puts it under that prefix (Message's
`MessageHub.Path`) and gets authentication with no host code. This lives here, on the one
`JwtBearerEvents` instance, because configuring `JwtBearerOptions` from anywhere else either misses the
named `Bearer` options entirely or overwrites these events.

## Rate limiting is one purpose-and-key primitive

`IAuthRateLimiter.CheckAsync(purpose, key, maxAttempts, window)` checks and increments **one**
dimension at a time. Endpoints that partition by more than one dimension (`register`, `login`, and
`forgot-password` check IP and identity, with endpoint-specific limits) call it once per dimension
and combine the decisions themselves — there is no single shape that fits every endpoint's table, since
`reset-password`/`resend-verification` are single-dimension and `change-password`/`change-email` are
authenticated-only (`user:{id}`, never IP). `AuthRateLimits` holds the fixed numbers; they are policy
decisions, not deployment knobs, so they are not configurable the way `LoginRateLimitOptions` is.

## Rules a change must not break

- The password hash never leaves the entity — not a DTO, not a claim, not a log.
- The refresh token's and every `AuthToken`'s plaintext is never persisted. `IssuedRefreshToken` /
  `IssuedAuthToken` are the one moment both halves coexist: the value goes to the client or the email,
  the digest to the row.
- Rotation is one `SaveChangesAsync`, or a crash strands the session.
- **`ITokenService` never says no.** It mints and digests; whether a caller *deserves* a token is
  `IAuthService`'s business. A token service that can refuse is a second, invisible authz layer.
- **Auth references only foundation (`User`, `Postgres`, `Cache`) and no feature service.** Its mail
  goes through its own `IAuthEmailSender` port; `Apps/Api/Integrations/AuthEmailSender.cs` fills it
  with Email's templates. The port is required, so a host without that bridge fails at startup.
- `User.Audience(permission)` stays in `Services/User/Extensions/`. Moving it here would be a cycle.
- **A mailed link or notice is never a precondition for the write that triggered it.** The `IAuthEmailSender`
  bridge only queues the email in the Email outbox; delivery and its retries happen later in the Email
  worker, so a provider outage never reaches an auth flow. A failed queue result is logged, and
  `resend-verification`/`forgot-password` stay the recovery path for a mail that never arrived.
- **Every mass-revocation flow stamps `User.SessionsRevokedAt` before calling `IssueSession`**, using
  the same `clock.GetUtcNow()` value for both, so the freshly-issued token's `iat` is never strictly
  before the stamp it would otherwise fail against.

## Decisions, and what was deliberately not built

- **Refresh token in the body, not a cookie.** Mobile is the primary client and has no cookie jar;
  `Apps/Web` wraps the returned session in its own HTTP-only cookie server-side, so browser JS never
  sees a token. Better than the API guessing which transport a caller wants.
- **HS256, not RS256.** One host signs and verifies. Asymmetric keys buy nothing until a second
  party must verify without being able to mint.
- **`AuthOutcome` / `AuthActionOutcome` (`readonly record struct`s) over exceptions.** A failed login
  or a bad token is an expected outcome; throwing makes the normal path pay for the abnormal one and
  puts typos in the error logs. `AuthActionOutcome` is the same idea for the endpoints that return no
  session (verify-email, change-email, confirm-email-change, session revocation).
- **Enumeration safety over honest error messages, on purpose.** `forgot-password` and
  `resend-verification` are 204-always — a registered address, an unregistered one, and a
  rate-limited one are indistinguishable on the wire. The client copy says "if that address has an
  account…" deliberately hedged; the alternative is a free account-existence oracle.
- **`RequireVerifiedEmail` gates exactly one action: Message `Send`.** Everything else an account can do stays open to an unverified
  caller, so do not re-add the gate without a product decision.
- **No OAuth/social sign-in, no 2FA/TOTP, no magic links, no passkeys, no login-history table, no
  breached-password lookup, no "this wasn't me" link in a notice mail, no auth audit log, no
  admin-initiated password reset, no password history/forced rotation.** Each was considered and
  explicitly deferred rather than overlooked.
- A password hash never appears in a DTO.
- **Names are stored and sent split.** `RegisterRequest` and `AuthUserDto` carry
  `FirstName` and `LastName`; the token carries `given_name` and `family_name`. A
  client joins them for display. There is no combined name column or field to fall back to.

## Shared authorization contract

Feature services may reference `Auth/Contracts` for claims and `User.HasPermission`; they must not
reference the Auth implementation for these helpers. Restricted controllers use
`[Authorize(Policy = AccessPermissions.X)]`, backed by one policy per entry in
`AccessPermissions.All`. Services still check the acting account's current permission themselves.
Self-service actions on the caller's own rows need `[Authorize]` and ownership checks rather than
an operator permission.

Never authorize with `[Authorize(Roles = ...)]`, `IsInRole`, or a `UserRole` comparison. The `role`
claim is a display label, and custom roles carry `Client` there; permission claims and the saved
role row determine access.

## Saved role permissions

JWT validation reads the assigned role and current permission array from Postgres on every request,
then replaces permission claims alongside the existing role and profile claims. Signup assigns the
seeded Client role row. A role edit therefore takes effect on the next authenticated HTTP request.

`AuthUserDto` carries `RoleName` and `Permissions`, read from the same row, on `GET /api/auth/me` and
in every issued session. A client uses them to show only what the account may do; they authorize
nothing. The `role` claim and `AuthUserDto.Role` are the legacy two-value label and are `Client` for
every custom role.
