# MCP API proxy service

## Goal

Expose each company's existing `/api/*` endpoints as an always-on, company-isolated MCP server.

## Scope

- Add the hidden, platform-tagged MCP service with the official ASP.NET Core MCP SDK and documentation.
- Serve `/mcp` using bearer-token authentication; no configured token returns 404.
- Generate MCP tools from active API endpoints and execute them through loopback HTTP so existing authorization and service gating apply.
- Generate a per-company MCP token in protected scaffold configuration.
- Document the Owner-scoped automation principal explicitly.

## Task-specific tests

- Assert missing and invalid tokens return 401 and an unconfigured token returns 404.
- Use an MCP SDK client to complete initialization and verify known API tools appear in `tools/list`.
- Invoke a tool for a disabled service endpoint and assert the proxied 404 is preserved.
- Runtime-load a service and assert tool inventory refreshes, or test and document the explicit refresh trigger.
- Scaffold two companies and assert each MCP endpoint rejects the other company's token.
