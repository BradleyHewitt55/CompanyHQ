# Repository structure

> **Document status:** Target V1 recommendation; no monorepo exists yet.

```text
apps/
  web/                 # Next.js public site and /admin route groups
  worker/              # BullMQ CMS, agent, indexing, media, and webhook jobs
  scheduler/           # Repeatable-job registration and schedule reconciliation
  cli/                 # Local provisioning and operations CLI
packages/
  contracts/           # Zod schemas, DTOs, commands, events, job/tool contracts
  config/              # collection/global/field/admin configuration compiler
  cms-core/            # collection operations and mutation pipeline
  admin-ui/            # generated admin registry and override slots
  auth/                # sessions, authentication, principal construction
  authorization/       # central RBAC/ABAC evaluator and query constraints
  company-context/     # installation context validation and propagation
  database/            # PostgreSQL adapters, repositories, migrations, outbox
  storage/             # installation-scoped media and artifact adapters
  versions/            # revisions, drafts, restore, optimistic concurrency
  workflows/           # review, approval, publishing state machines
  agents/              # provider-neutral lifecycle, profiles, memory, budgets
  agent-adapters/      # Codex, Claude, Pi, Hermes adapters
  agent-tools/         # MCP/tool catalog and command translation
  orchestration/       # tasks, runs, leases, recovery, dependencies
  queue/               # BullMQ schemas, namespacing, retries, DLQ conventions
  audit/               # immutable security/business audit events
  observability/       # OpenTelemetry, logs, metrics, redaction
  source-control/      # project registry, Git/worktree and provider adapters
  extensions/          # hooks, plugins, custom endpoints and component registry
  testing/             # fixtures, contract tests, isolation/security helpers
infra/
  docker/              # versioned Compose templates and health checks
docs/                  # product contracts, decisions, studies, roadmap
```

Only `database` repositories and narrowly defined application services may access PostgreSQL. UI, adapters, tools, hooks, and agents cannot import database clients. Import-boundary lint rules and architecture tests enforce this.
