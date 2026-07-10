# Custom service creation

> **Document status:** Target V1 extension guide.

A custom service is a source-backed, versioned CompanyHQ extension. A human developer and an agent use the same service contract, Change, worktree, GitHub pull request, test gate, approval, deployment, and audit trail.

## Creation paths

| Path | Result |
| --- | --- |
| Manual development | A developer creates a service Change in an isolated company-root worktree. |
| Agent-assisted development | An authorized agent prepares the same Change within the delegating user's effective access. |
| CompanyHQ-supplied service | A platform module that a company can configure or enable by class. |

No path permits unregistered runtime schema or route mutation.

## Naming and location

Choose a unique lowercase kebab-case service key. Do not use display labels, provider trademarks, or a path from another service as the key.

~~~text
Admin/Services/<service-key>/
  Pages/
  Settings/
  Service/
  Models/
~~~

The service key owns references beginning with service.<service-key>. A service detail page uses the parent page reference plus (slug), following the Explorer contract.

## Mandatory declarations

Before an agent or developer can request service enablement, the manifest/contract declares:

- owner, version, compatibility, dependencies, conflicts, runtime, provider adapters, enable/disable/remove rules;
- model attributes, DTOs, admin/client exposure, field policy, migrations, data retention, backup, and rollback;
- human roles and the exact delegated-agent access intersection; agent actions cannot exceed the requesting user;
- admin/client page references, list/detail routes, visual handoff, source ownership, and notification events;
- versioned API request/response DTOs, MCP parity, input/output constraints, rate/abuse category, approval, and audit;
- hooks, durable jobs, webhook verification, health checks, failure recovery, and observability;
- task-specific API, UI, policy/DTO, MCP, security, migration, provider, and recovery tests;
- affected documentation and extension-governance reference updates.

Secrets are references only. Their values never appear in source, Explorer metadata, Git, logs, prompts, DTOs, MCP, screenshots, or notifications.

## Delivery workflow

1. Create a Change with outcome, service key, company, acceptance criteria, delegating user, and approval class.
2. Validate key, model/DTO, page, detail variant, route/API version, MCP, dependencies, role/agent scope, and source-control collisions.
3. Prepare source, migration, service contract, tests, docs, and safe worktree preview.
4. Review field exposure, agent access, enable/disable behavior, backup, security, and rollback.
5. Prepare the worktree diff and evidence; a human may separately approve commit/push/pull-request creation, merge, and deployment, then record the health check and rollback point.

## Documentation is mandatory

Creating/updating a service also updates the service contract, Explorer, models/DTOs, routing/API versioning, access control, agents/MCP, hooks, pipeline/notifications, source control, testing, security, and the repository-local extension-governance skill where relevant. Missing documentation blocks approval.
