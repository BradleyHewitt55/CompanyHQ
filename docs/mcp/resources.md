# MCP resources

> **Document status:** Target V1 registry contract.

Resources expose authorized, bounded, read-only views of CMS schemas, DTOs, tasks, subtasks, projects, worktrees, diff summaries, agent metadata, skills, rules, documentation, and execution history. URIs use opaque stable IDs, never host paths.

Each resource declares version, MIME type, maximum size, cache/etag behavior, policy, DTO, classification, and pagination. Reads reauthorize and redact; subscriptions must reauthorize on delivery.
