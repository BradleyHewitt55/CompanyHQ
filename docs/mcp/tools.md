# MCP tools

> **Document status:** Target V1 registry contract.

Tool names use `companyhq.<domain>.<verb>` and declare version, purpose, input/output Zod DTO, capability, risk/approval class, idempotency, deadline, rate class, audit event, handler, and safe error mapping. Mutations translate into typed application commands.

Categories are CMS reads/writes, collection queries, drafts, updates, publishing, media, tasks/subtasks, project status, source status, bounded worktree read/write, diff, approved test/build execution, and agent connection status. Arbitrary SQL, shell, path, URL-fetch, Docker, Redis, credential, merge, deploy, and approval tools are forbidden.
