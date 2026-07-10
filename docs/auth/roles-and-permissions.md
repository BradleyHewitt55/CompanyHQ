# Roles and permissions

> **Document status:** Confirmed Target V1 role model.

| Role | Default scope |
| --- | --- |
| `owner` | Installation ownership, critical policy, recovery, roles, releases, and all explicitly granted domains. |
| `admin` | Day-to-day CMS, users, workflows, services, projects, tasks, and agents except owner-only controls. |
| `dev` | Config, extensions, source changes, projects, previews, and technical diagnostics; no automatic content/user/deploy authority. |
| `client` | Assigned content, review, approval, reports, and tasks only. |

Roles are bundles of operation grants, not conditionals scattered through code. ABAC conditions narrow grants by collection, record, field, project, workflow state, ownership, locale, environment, authentication strength, time, and approval. Default is deny.

Agent roles are capability profiles, not human application roles. Effective agent access is the intersection of delegator permissions, agent profile, provider health, task, project/collection/resource scope, worktree, tool, environment, budget, network policy, and live approval.

## Default permission matrix

`A` means allowed by role but still narrowed by ABAC; `O` means owner-only by default; `S` means assigned scope only; `-` means denied. Installations may narrow these defaults, never silently broaden protected actions.

| Area | owner | admin | dev | client |
| --- | --- | --- | --- | --- |
| Admin access | A | A | A | A |
| User/invitation management | A | A | - | - |
| Role/policy management | O | - | - | - |
| Collections/content | A | A | S | S |
| Field/schema configuration | A | S | A | - |
| Publishing/content approval | A | A | - | S |
| Media | A | A | S | S |
| Extensions/plugins | A | S | A | - |
| Custom endpoints | A | S | A | - |
| Agent connections/profiles | A | S | S | - |
| Agent Chat | A | A | S | S |
| Tasks/subtasks | A | A | S | S |
| Pipelines/DLQ operations | A | A | S | - |
| Project connections | A | A | S | - |
| Source-control status/diff | A | A | S | - |
| Worktree create/write | A | S | S | - |
| Commit/push/PR request | A | S | S | - |
| Merge/protected branches | Human owner/admin policy only | Human policy only | - | - |
| MCP configuration/keys | O | S | - | - |
| Environment/secrets | O | - | - | - |
| Audit logs | A | S | S | - |
| Production migration/deploy/restore | O | Explicit approval only | - | - |

Collection, record, field, project, workflow, environment, and approval attributes always apply. The provisioned account receives `owner`.
