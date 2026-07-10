# Routing and API ownership

Routes are registered by the control plane and approved service/project manifests. A tenant, service, or agent may not reserve a route by creating a file. Route collisions fail validation before a Change can be approved.

## Reserved route families

| Family | Owner | Purpose |
| --- | --- | --- |
| /admin | Admin shell | Authenticated CompanyHQ workspace, Page Catalog, Explorer, and personal settings. |
| /admin/services | Service administration | Service catalogue, enablement, health, settings, and history. |
| /admin/projects | Project administration | Project registry, worktrees, deployments, and source status. |
| /api | Product API gateway | Versioned human/application operations with policy-projected DTOs. |
| /mcp | CompanyHQ MCP gateway | Authenticated remote MCP transport. |
| local MCP transport | CompanyHQ MCP gateway | Company-scoped delegated-agent transport. |
| /hooks | Integration ingress | Verified provider callbacks, never a general public API. |
| project public routes | Project deployment | Customer-facing website/app routes, owned by that project repository. |

## Admin page and detail routes

The list/main page is the parent reference. Its detail view is the (slug) variant:

~~~text
/admin/users
/admin/users/(slug)

/admin/services/<service-key>/<page-key>
/admin/services/<service-key>/<page-key>/(slug)
~~~

The (slug) notation is the CompanyHQ source-template convention. It represents a runtime URL segment resolved by an authorized operation. A slug is never an authorization key; the resolved record, company, role, and field policy decide access.

## Versioned API routes

Service APIs use:

~~~text
/api/v<major>/services/<service-key>/<resource-key>
~~~

Each operation registers request/response DTO versions, human and delegated-agent policy, route visibility, abuse/rate category, audit event, and MCP mapping. APIs never return raw models. See [API versioning](api-versioning.md).

## API and MCP parity

Every supported API operation declares an MCP mapping. The mapping can be an executable tool, safe inspection resource, or controlled prompt/initiation flow. It must never weaken interactive authentication or turn a webhook, health probe, redirect, or credential flow into an unsafe agent tool.

Tool names are stable and service-scoped:

~~~text
companyhq.<service-or-domain>.<operation>
~~~

The API and MCP handler reauthorize independently and use compatible DTO versions. An API version without an MCP declaration, or an MCP tool without a registered control-plane/API contract, fails service validation.

## Routing rules for projects

Projects own public routes. The admin control plane owns administrative routes. An admin override cannot silently change a public project route, and a project cannot serve CompanyHQ administration.

Links use registered company, project, page, service, record, and API version identifiers rather than filesystem paths. This makes source-control moves, independent repositories, deployment replacement, and rollback traceable.
