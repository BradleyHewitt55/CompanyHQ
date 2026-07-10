# Routing and API ownership

> **Document status:** Target V1 architecture contract.

One Next.js application serves the company domain. Registered configuration owns routes; creating a file or extension cannot silently reserve a URL. The compiler rejects collisions before deployment.

| Route | Owner and behavior |
| --- | --- |
| `/` and public catch-all | Published page/route registry; public DTOs only. |
| `/admin` | Authenticated dashboard. |
| `/admin/login`, `/admin/recover`, `/admin/mfa` | Minimal unauthenticated/auth-challenge layouts. |
| `/admin/collections/{collection}` | Authorized collection list/create. |
| `/admin/collections/{collection}/{id}` | Detail/edit, workflow, revisions, restore. |
| `/admin/globals/{global}` | Authorized singleton editor/history. |
| `/admin/media` | Media list/upload/usage/detail. |
| `/admin/users` | Users, invitations, sessions, roles within policy. |
| `/admin/tasks`, `/admin/tasks/{id}` | Tasks, subtasks, run/evidence/review. |
| `/admin/pipelines` | Queued/running/review/DLQ operational views. |
| `/admin/agents/chat/{thread}` | Agent Chat. |
| `/admin/agents/connections` | Provider instances/profiles/health. |
| `/admin/projects`, `/admin/projects/{id}` | Project registry, environments, status. |
| `/admin/projects/{id}/source` | VCS/hosted-provider status and review actions. |
| `/admin/projects/{id}/worktrees/{task}` | Task worktree/diff/checkpoint view. |
| `/admin/extensions` | Extension install/configuration/health. |
| `/admin/settings/*` | Company, auth, CMS, storage, queue, MCP, audit settings. |
| `/admin/custom/{registeredPath}` | Collision-checked custom admin views. |
| `/api/v{major}/*` | Versioned external/admin application API. |
| `/mcp` | Optional remote transport for the installation-local MCP server. |
| `/hooks/{provider}` | Signature-verified integration ingress. |

Admin uses a catch-all route resolver backed by the compiled manifest; slugs/IDs select resources but never authorize them. Server components query through application services. Client forms submit typed commands that reauthenticate and reauthorize.

Public/project routes cannot serve admin modules, and admin overrides cannot silently alter public routes. Every API operation declares authentication, action, request/response DTO, limits, idempotency, audit, and any MCP mapping.
