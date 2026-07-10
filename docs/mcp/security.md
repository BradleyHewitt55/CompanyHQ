# MCP security

> **Document status:** Confirmed Target V1 security contract.

Threats include token theft/replay, prompt injection, confused deputy, schema smuggling, path/command injection, SSRF, oversized results, timing/existence leaks, cross-installation IDs, stale tasks, malicious extensions, provider compromise, and data exfiltration.

Controls are default-deny discovery/execution, short-lived audience-bound capabilities, per-call policy, strict schemas, DTO allowlists, path canonicalization, registered commands, egress allowlists, rate/size/deadline limits, cancellation, approval binding, sandboxing, redaction, immutable audit, key rotation, and cross-company tests. Direct database tools are prohibited.
