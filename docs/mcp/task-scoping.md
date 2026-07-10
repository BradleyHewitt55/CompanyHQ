# MCP task scoping

> **Document status:** Confirmed Target V1 contract.

Task-scoped credentials name allowed actions, collections/documents, project, canonical worktree, commands, networks, budgets, expiry, and approval obligations. Child-task scope can only narrow the parent; source-changing subtasks use the parent worktree.

Cancellation, reassignment, role/profile disablement, worktree lease loss, approval expiry, or terminal task state revokes capability. Every call checks the live task/run attempt so stale workers cannot act.
