# Subtasks and dependencies

> **Document status:** Confirmed Target V1 contract.

Subtasks form an acyclic dependency graph. One parent task owns one worktree; all source-changing subtasks for that task share it and use serialized file/source-control coordination. Non-source subtasks may execute concurrently when dependencies and resource locks allow.

Child scope must be equal to or narrower than the parent. Completion does not merge, deploy, publish, or approve. Failed/cancelled dependencies block downstream claims until a human changes the graph.
