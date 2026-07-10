# Services and plugins

CompanyHQ calls an installed business capability a service. Its source module may be called a plugin internally, but the product surface uses Service consistently. Services extend CompanyHQ through a versioned, policy-aware contract; they do not become arbitrary runtime patches.

## Core versus optional

| Class | Initial members | Enablement |
| --- | --- | --- |
| Core | auth, users, forms, SEO | Always supplied. Configure and restrict them; do not turn them off. |
| Optional integration | email service, Resend provider adapter, Cloudflare R2, Google Merchant | Absent until an owner enables them after dependency/credential/policy checks. |
| Optional business service | ecommerce and future domain services | Absent until enabled with data, access, DTO, API/MCP, test, and rollback review. |
| Custom service | Company-authored or CompanyHQ-authored extension | Created as source in the company root repository and follows the same lifecycle. |

Resend and ecommerce are intentionally not default services. Email is provider-neutral, so a company can replace Resend later without changing the CMS service contract.

## Toggle system

The Admin Services page is the authoritative toggle surface. It lists available, installed, enabled, disabled, unhealthy, blocked, update-available, and archived service states along with dependency, health, ownership, version, and latest Change.

Enabling is never a bare boolean. It creates a Change that validates:

- service dependencies/conflicts, Docker/runtime requirements, health, secret references, and retention;
- human roles, delegated-agent scope, approvals, and audit;
- models, DTOs, field visibility, migrations, and backup/rollback impact;
- admin/client pages, same-name override references, routes/API versions, and MCP mappings;
- hooks/jobs/webhooks, notifications, test plan, preview, and recovery behavior.

Disabling removes active runtime registration after dependency checks. It preserves source/data by default. A tenant UI override remains versioned but inactive until the owning optional service is enabled again.

## Source layout

Custom and tenant-owned service source lives in the company root repository:

~~~text
Admin/Services/<service-key>/
  Pages/
  Settings/
  Service/
  Models/
~~~

Built-in service implementation remains supplied base code and is not exposed in a tenant Explorer. A user can create a same-name page override only when needed. See [Explorer overrides](../admin/explorer-overrides.md).

## Required service contract

Every service declares:

- stable key, label, owner, version, source, dependencies, conflicts, and enable/disable/archive behavior;
- models, attributes, DTOs, field policy, migrations, retention, backups, and recovery;
- human roles, delegated-agent access, task/approval limits, and audit events;
- admin/client pages, detail variants, source-visible references, and notification impact;
- versioned APIs, request/response DTOs, MCP tools/resources/prompts, input/output limits, and abuse controls;
- hooks, jobs, webhooks, health checks, runtime requirements, and provider adapters;
- API, UI where applicable, policy/DTO, MCP, security, migration, recovery, and integration tests;
- documentation and extension-governance skill updates.

An agent creating a service must fulfill this contract before it may request approval or enablement.

## Initial optional services

- [Email service](email.md) and [Resend provider adapter](email-resend.md)
- [Ecommerce](ecommerce.md)
- [Cloudflare R2 media storage](cloudflare-r2.md)
- [Google Merchant](google-merchant.md)
- [Custom service creation](custom-service.md)
