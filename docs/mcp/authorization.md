# MCP authorization

> **Document status:** Confirmed Target V1 security contract.

MCP uses the central authorization service. Effective access is human/service principal ∩ agent role/profile ∩ task/run ∩ tool/resource/prompt ∩ project/collection/worktree ∩ action/field ∩ approval ∩ expiry/budget/rate policy.

Session creation is not authorization for later calls. Discovery filters names and schemas; execution rechecks current state and emits a decision record. Marketing/research profiles cannot receive source-control tools. Coding profiles receive only the assigned project/worktree. No profile receives database tools.
