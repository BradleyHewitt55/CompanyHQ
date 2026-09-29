# MCP service

`Services/Mcp` owns agent credentials, OAuth, and the `/mcp` transport. Each MCP adapter calls the
same domain service as its marked HTTP action. The API action supplies the stable tool name,
permission, and agent-facing description through `McpActionAttribute`.

## Rules

- `pgk_` and OAuth access tokens authenticate only at `/mcp`; JWTs authenticate the HTTP API.
- The connection owner and optional connection role are read from Postgres on every MCP request.
  Permissions are the intersection of the owner's current role and the connection's assigned role.
  The key's `ReadOnly` or `Write` access and deny list may only reduce this set.
- `McpActionRegistry` discovers marked actions at startup and requires exactly one adapter with the
  same name. Unmarked actions are absent from discovery and denied by `McpAccess` on direct calls.
  Keep `tools/list` filtering and `tools/call` authorization together.
- Tool descriptions come from the marked API action. Parameter descriptions belong on the adapter.
  A description must tell an agent the operation, scope, and relevant paging or side effect.
- Only Blog, FAQ, email templates, area search, and own notification preference actions are currently
  marked. `WhoAmI`, user search, message, and moderation have no adapters.

## Decisions

- Role rows live in User. An agent key may assign a different row from its owner, but may never gain
  permissions the owner lacks. `PUT /api/agent-keys/{id}/role` changes an assignment, and null resumes
  inheritance. The plaintext key is shown only when issued.
- A client permission controls each tool, including reads. `ReadOnly` keys cannot call write tools.
  Domain services still enforce their ownership and publication rules after the MCP filter.
- The existing per-account and per-key disabled-tool arrays remain narrowing controls for older
  connections; they do not grant access.

## Traps and wiring

- Mcp references only `Auth/Contracts`, `User` and `Postgres`. Key and OAuth secrets are encoded with
  `WebEncoders.Base64UrlEncode` from the shared framework; do not pull in the JWT package for it.
- Agent keys go through `IPostgresService<T>`. Only `McpOAuthService` takes `PostgresDbContext`,
  because code exchange and refresh rotation are guarded single-use updates inside a transaction.
- Call `AddMcp` after controller modules register their MVC application parts; action discovery reads
  those parts. The host maps the endpoint with `app.MapMcpEndpoint()`, which keeps `/mcp` outside
  `MapControllers` and requires `McpPolicies.Agent` — a user's JWT is not an agent credential and gets
  a 401 there.
- `AddMcp` owns the anonymous client-registration rate limit (`McpOAuthRateLimits.ClientRegistration`,
  20 per hour per address).
- `Mcp:OAuth:PublicWebBaseAddress` falls back to `Auth:WebBaseAddress` when unset — one public web
  origin serves both sign-in links and the consent page, so it is configured once.
- Do not add `.WithTools<T>()` registrations. Mark an API action, provide an adapter over its domain
  service, and give both the same stable name. A missing or duplicate adapter fails startup.
- The MCP list filter is only discovery. Direct `tools/call` must check the permission again.
