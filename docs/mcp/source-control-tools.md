# MCP source-control tools

> **Document status:** Target V1 tool contract.

Allowed tools include scoped status, refs, diff, checkpoint, task-branch/worktree metadata, and policy-gated commit/push/PR preparation. Tools act only on the registered repository and assigned worktree and return structured results.

No MCP tool can merge, approve, force-push, change protected branches/settings, manage credentials, delete remote repositories, or deploy. Commit, push, and PR creation require distinct permissions and approvals.
