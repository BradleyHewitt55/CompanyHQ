# Architecture decision records

> **Document status:** Confirmed Target V1 decisions.

| ADR | Decision |
| --- | --- |
| [001](001-independent-company-installations.md) | Independent company installations |
| [002](002-typescript-monorepo.md) | TypeScript pnpm monorepo |
| [003](003-nextjs-web-admin.md) | One Next.js public/admin application |
| [004](004-postgresql-hybrid-content.md) | PostgreSQL relational + JSONB content |
| [005](005-database-access-boundary.md) | Application-service database boundary |
| [006](006-company-context.md) | Central company context |
| [007](007-redis-bullmq.md) | Redis and BullMQ jobs |
| [008](008-compiled-cms-configuration.md) | Compiled CMS configuration |
| [009](009-admin-registry-overrides.md) | Registry-driven admin and sparse overrides |
| [010](010-rbac-abac.md) | Central RBAC/ABAC |
| [011](011-revisions-workflows.md) | Immutable revisions and workflows |
| [012](012-agent-provider-adapters.md) | Provider-neutral agent adapters |
| [013](013-mcp-mandatory.md) | MCP mandatory in V1 |
| [014](014-agent-capabilities.md) | Intersection-based agent capabilities |
| [015](015-human-approval.md) | Bound human approvals and separation of duty |
| [016](016-task-worktrees-no-agent-merge.md) | One task worktree; no agent merge |
| [017](017-storage-boundary.md) | Installation-scoped storage adapters |
| [018](018-outbox-audit.md) | Transactional outbox and immutable audit |
| [019](019-opentelemetry.md) | OpenTelemetry observability |
| [020](020-public-extension-sdk.md) | Versioned public extension SDK |
| [021](021-user-triggered-short-lived-runs.md) | User-triggered short-lived runs |
| [022](022-hermes-local-remote.md) | Hermes local and remote connections |
| [023](023-full-cms-v1.md) | Full CMS scope in V1 |
| [024](024-native-agents-v2.md) | Native CompanyHQ agents deferred to V2 |
| [025](025-dto-only-boundaries.md) | DTO-only external boundaries |
| [026](026-source-control-v1.md) | Source control is a V1 feature |

All ADRs are accepted and should be reviewed by 2027-01-15 or earlier when implementation evidence invalidates an assumption.
