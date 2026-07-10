# MCP authentication

> **Document status:** Target V1 security contract.

Local workers receive a one-use bootstrap credential over a protected channel and exchange it for short-lived task/run/session capabilities. Remote clients use TLS and a scoped API key or signed short-lived token; API keys are hashed, rotation-capable, and never put in URLs.

Tokens bind issuer, installation, audience, subject/delegator, agent/connection/profile, task/run, scopes, nonce, issued/expiry times, and key ID. Expiration, capability revocation, session revocation, user/provider disablement, and key rotation are checked on every call.
