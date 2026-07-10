# Target repository assessment

> **Document status:** Verified repository finding as of 2026-07-10, followed by Target V1 recommendations.

## Verified current state

The CompanyHQ target contains architecture/product Markdown, project instructions, three read-only reference repositories under `.repos/`, and an `ExamplePages/company-dashboard.html` concept. It has no package/workspace manifest, application source, database migrations, tests, Docker/CI/deployment configuration, runtime entry point, generated admin, API, authentication, queue, worker, MCP server, or agent implementation. The target directory is not currently a Git repository.

The existing documentation already covered admin concepts, auth variants, content lifecycle, forms/media/SEO, agent profiles/orchestration, services, models/DTOs, hooks, tasks/pipeline, source control, security, and tests. Several documents previously described a central control plane, company switcher, different role set, and incompatible provisioning flags; this pass replaces those as authoritative contracts.

## Initial implementation gaps

| Severity | Gap | Impact and acceptance direction |
| --- | --- | --- |
| Critical | No runnable architecture or enforcement | Build Phase 1 boundaries, then company context/auth/database before CMS or agents. |
| Critical | No central mutation/policy pipeline | Every transport must use context-bound application commands with version/audit/idempotency. |
| Critical | No independent-installation runtime | Two installations must have distinct Compose/network/database/Redis/storage/keys/MCP and pass isolation tests. |
| Critical | No agent/MCP sandbox | No provider runs until task capabilities, MCP per-call policy, and direct-DB/tool prohibitions are tested. |
| High | No versions/workflows/approvals | Content/agent mutations cannot be production-safe without immutable revisions and bound approvals. |
| High | No BullMQ/recovery | Long-running work would be lossy/duplicated without outbox, leases, fencing, DLQ, and reconciliation. |
| High | No source/worktree implementation | Coding agents lack safe isolation and review evidence. |
| High | No audit/observability | Actions cannot be attributed, debugged, or recovered. |
| Medium | Static concept uses legacy labels/broken links | Treat as non-authoritative design input; replace during admin implementation. |

## Architectural debt to avoid

Do not turn target documents into one giant package, serialize database models, add per-caller company filters, let extensions import internals, use Redis/provider state as truth, allow raw shell/SQL tools, or implement adapters before canonical contracts. The [roadmap](implementation-roadmap.md) is the required implementation order.
