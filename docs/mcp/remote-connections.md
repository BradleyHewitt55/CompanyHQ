# Remote MCP connections

> **Document status:** Target V1 security contract.

Remote MCP requires TLS, installation-specific audience, scoped API key exchange or short-lived token, origin/client rate controls, replay-resistant nonce, bounded concurrent sessions, idle/absolute expiry, and revocation. Mutual TLS is supported for managed integrations.

Network exposure is opt-in. Reverse proxies must preserve authenticated client/correlation metadata through signed trusted headers and strip untrusted copies. Remote transport never broadens tool scope.
