# CompanyHQ documentation

> **Document status:** Confirmed documentation index. Product behavior is Target V1 unless a document says otherwise.

CompanyHQ is an original, independent CMS installed once per company. The repository currently contains documentation and an HTML concept only; implementation starts with [roadmap Phase 1](implementation-roadmap.md#1-monorepo-foundations).

## Start here

- [Document status vocabulary](document-status.md)
- [Glossary](glossary.md)
- [Repository assessment](repository-assessment.md)
- [Repository structure](repository-structure.md)
- [Architecture overview](architecture/overview.md) and [all diagrams](architecture/diagrams.md)
- [Implementation roadmap](implementation-roadmap.md)
- [ADR index](decisions/README.md)

## Architecture and operation

- [Company isolation](architecture/company-isolation.md), [runtime boundaries](architecture/runtime-boundaries.md), [data flow](architecture/data-flow.md), [request flow](architecture/request-flow.md)
- [Routing](architecture/routing.md), [server layout](architecture/server-layout.md), [deployment](architecture/deployment.md), [development/production](architecture/development-and-production.md)
- [Provisioning](provisioning/overview.md), [CLI](provisioning/cli.md), [installation](provisioning/company-installation.md), [upgrades](provisioning/upgrades.md), [backup/restore](provisioning/backup-and-restore.md)
- [Audit](operations/auditing.md), [observability](operations/observability.md), [logging](operations/logging.md), [health](operations/health-checks.md), [recovery](operations/failure-recovery.md)

## CMS, admin, and identity

- [Configuration](cms/configuration.md), [collections](cms/collections.md), [fields](cms/fields.md), [database adapters](cms/database-adapters.md), [DTOs](cms/dto-contracts.md)
- [Admin routing](cms/admin-routing.md), [overrides](cms/admin-overrides.md), [hooks](cms/hooks.md), [drafts/versions](cms/drafts-and-versions.md), [publishing](cms/publishing.md), [extensions](cms/extensions.md), [custom endpoints](cms/custom-endpoints.md)
- [Authentication](auth/authentication.md), [roles](auth/roles-and-permissions.md), [access control](auth/access-control.md), [authorization](auth/authorization.md)

## Tasks, agents, MCP, and projects

- [Tasks](tasks/overview.md), [brain dump](tasks/brain-dump-to-task.md), [subtasks](tasks/subtasks.md), [BullMQ](tasks/bullmq.md), [recovery](tasks/recovery.md), [audit](tasks/auditing.md)
- [Agent connections](agents/connections.md), [capabilities](agents/roles-and-capabilities.md), [chat](agents/agent-chat.md), [Codex](agents/codex.md), [Claude](agents/claude.md), [Pi](agents/pi.md), [Hermes](agents/hermes.md), [reconnection](agents/reconnection.md), [security](agents/security.md)
- [MCP documentation suite](mcp/overview.md)
- [Projects](projects/overview.md), [source control](projects/source-control.md), [worktrees](projects/git-worktrees.md), [task workflow](projects/task-workflow.md), [review](projects/review-flow.md)

## Security and research

- [Threat model](security/threat-model.md), [secrets](security/secrets.md), [agent boundaries](security/agent-boundaries.md), [source-control boundaries](security/source-control-boundaries.md), [cross-company tests](security/cross-company-tests.md)
- [Payload study](reference-studies/payload-cms.md), [Hermes study](reference-studies/hermes-agent.md), [T3 Code study](reference-studies/t3code-source-control-and-agents.md)
- [Documentation inventory](documentation-inventory.md)

Older topical documents remain as supporting detail. Where two documents overlap, the focused document linked above is authoritative.
