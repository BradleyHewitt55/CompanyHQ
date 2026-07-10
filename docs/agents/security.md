# Agent security

> **Document status:** Confirmed Target V1 security contract.

Agents execute in isolated containers/processes with a read/write mount for only the task worktree and bounded artifact directory. No database/Redis socket, Docker socket, host filesystem, `.env`, sibling installation, unrestricted shell, arbitrary network, or raw secrets are exposed.

Tool allowlists, typed inputs, path canonicalization, egress proxies, secret brokers, output limits, timeouts, cancellation, malware/content classification, prompt-injection labeling, approval obligations, and audit apply to every call. See [agent boundaries](../security/agent-boundaries.md).
