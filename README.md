# CompanyHQ

> **Document status:** Target V1 product contract. The repository currently contains architecture documentation and an HTML concept only; no CompanyHQ runtime has been verified.

CompanyHQ is an independently installed, agent-driven CMS. Every company receives its own application, PostgreSQL database, Redis/BullMQ runtime, storage boundary, secrets, queues, projects, worktrees, audit trail, and MCP server. There is no shared company switcher or central multi-company control plane.

The product target is a TypeScript monorepo using Next.js, React, PostgreSQL, Redis, BullMQ, Zod, and Docker Compose. Humans and agents use the same validated application services. Agents never receive direct database access and never merge, approve, or deploy their own work.

It is not a fork, dependency, merger, or rebrand of any reference project, and it is not a central SaaS management panel. Reference repositories under `.repos/` are read-only architectural research; CompanyHQ uses original implementations and stable public contracts.

## Product shape

Each installation serves the public website at `/` and authenticated CMS at `/admin`. V1 includes config-driven collections, globals and fields; generated and custom admin views; users and owner/admin/developer/client roles; APIs; drafts, immutable revisions, approvals and publishing; media; hooks/extensions/custom endpoints; Redis caching; BullMQ tasks/schedules/recovery; external agent adapters and Agent Chat; mandatory MCP; projects, Git/source-host adapters and one worktree per task; audit, telemetry, provisioning, backup, upgrade, and restore.

External providers such as Codex, Claude, Pi, and Hermes connect through provider-neutral adapters. CompanyHQ remains authoritative for task/run state, capabilities, approval, tools, mutations, cost/rate controls, cancellation, and recovery. MCP exposes only typed, per-call-authorized application operations. Coding agents receive the assigned task worktree; marketing/research agents receive no source-control tools.

## Target workspace

```text
apps/       web, worker, scheduler, cli
packages/   contracts, config, CMS, auth/policy, database/storage,
            workflows, agents/MCP, queue/orchestration, source control,
            audit/observability, extensions, testing
infra/      Docker/Compose templates
docs/       architecture, security, decisions, studies, roadmap
```

See [repository structure](docs/repository-structure.md) for import and ownership rules.

Start with the [documentation index](docs/README.md), [architecture](docs/architecture/overview.md), [security model](docs/security/threat-model.md), [implementation roadmap](docs/implementation-roadmap.md), and [decisions](docs/decisions/README.md). These documents define the target architecture; they are not evidence that implementation exists.

## Repository state

| Area | Verified state |
| --- | --- |
| Product implementation | Not present |
| Package/workspace manifest | Not present |
| Database migrations | Not present |
| Automated tests | Not present |
| Deployment manifests | Not present |
| Architecture documentation | Present; this is the current source of truth |
| Reference repositories | Read-only under `.repos/` |

Do not infer implementation from a target-state document. Every document uses the status vocabulary in [document status](docs/document-status.md).
