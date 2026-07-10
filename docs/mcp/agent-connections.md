# MCP agent connections

> **Document status:** Target V1 contract.

Connection resources/tools report configured provider instance, adapter version, capability compatibility, health, rate-limit state, active task sessions, and safe reconnect/stop requests. Credential values and provider-global session lists are never exposed.

Starting a session requires enabled provider/profile, healthy adapter, runnable task, worktree lease where needed, budget, and minted task capability.
