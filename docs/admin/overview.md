# Admin workspace

> **Document status:** Target V1 product contract.

The admin workspace is the company operating surface. It starts from a supplied base UI but gives each company a sparse, version-controlled override layer, governed services, projects, staff access, tasks, agents, and source control.

The initial shell contains Dashboard, Pipeline, Users, Gallery, Keys, Skills, Agents, Services, Settings, and a website link. There is no company switcher because one admin belongs to one independent installation.

## Admin principles

- Supplied admin UI is rendered as a private fallback. Its source code is never shown, mounted, copied, or diffed for a tenant or agent.
- The Page Catalog uses the same page names as the supplied UI. A same-name tenant override file appears only after a user intentionally edits that page.
- Every view receives an access-filtered DTO, not a raw model. Admin, client, and agent contexts can differ by role and operation.
- Navigation, actions, data, component selection, Explorer entries, and notifications are policy-driven. Hiding an item is never the access-control mechanism.
- Manual edits and agent edits use the same Change, worktree, test, GitHub pull-request, approval, and rollback lifecycle.

## Primary areas

| Area | Purpose |
| --- | --- |
| Dashboard | Company health, operational metrics, activity, and approved quick actions. |
| Pipeline | Personal tasks, staff tasks, delegated-agent tasks, review, Change, and marketing status. |
| Users | Staff invitations, role assignments, status, access history, and user DTO pages. |
| Gallery | Managed media and its storage, usage, access, and retention state. |
| Keys | Secret references and integration health; never plaintext values. |
| Skills | Installed company/project agent skills with version, provenance, profile assignment, and enable/disable state. |
| Agents | Optional provider catalogue, company/project profiles, roles, delegated runs, approvals, and safe summaries. |
| Services | Core-service configuration plus an optional-service catalogue with explicit toggles. |
| Models & DTOs | Role-governed model attributes, field exposure, DTO versions, and page bindings. |
| Notifications | Per-user desktop/mobile preferences and mandatory security/approval notices. |
| Settings | Company profile, source control, deployment, backup, policy, and secure integrations. |
| Explorer | Main-branch source tree, Page Catalog, visual handoff, active Change worktrees, and history. |

## Service catalogue behavior

The Services page separates core services from optional services:

| Class | Initial members | Behavior |
| --- | --- | --- |
| Core | auth, users, forms, SEO | Always supplied. Configure and restrict them; do not turn them off. |
| Optional | email, Resend adapter, ecommerce, R2, Google Merchant, future services | Listed with dependencies, required credentials, health, version, owner, and an enable/disable action. |

Enabling a service creates a Change. It must show planned roles, delegated-agent access, DTOs, migration, routes/API versions, MCP mappings, UI contribution, secrets required, tests, and rollback effect before approval. Disabling removes runtime activation after dependency and retention checks; it does not erase source or data by default.

## Page editing

An owner/developer can inspect a page, hover a component, pin it, leave a comment, and pass the scoped work to an approved coding agent in a minimizable bottom chat bar. The handoff uses a stable component reference and safe DTO binding rather than copied base code or a brittle CSS selector.

For a tenant-owned component, the editor can show its source class/symbol as diagnostics. For a supplied component, it shows a public component reference and page contract only. See [visual agent handoff](visual-agent-handoff.md).

## Durable operations

Provisioning, service enablement, model changes, builds, agent runs, notifications, and deployments show durable status rather than a transient spinner. Refreshing the browser must not lose the stage, latest event, cancellation/retry guidance, Change, or approval state.

## Source visibility

The Explorer opens protected main in the current company root repository by default. Users deliberately switch to an active Change worktree to edit. A supplied page with no override appears in the Page Catalog but has no source file in the tenant Explorer.

Exact paths, detail-page naming, DTO binding, and deletion fallback are defined in [Explorer and overrides](explorer-overrides.md).
