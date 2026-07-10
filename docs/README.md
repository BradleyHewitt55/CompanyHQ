# CompanyHQ documentation

CompanyHQ is an agent-driven company CMS and application workspace. It gives each company isolated admin overrides, optional services, projects, staff access, delegated agents, source control, Docker-operated deployments, and a governed pipeline.

These documents define the services, functions, security boundaries, data flow, user flow, operational behavior, and completion evidence without selecting a programming language or application framework.

## Product rules

- CompanyHQ is the canonical product, tenant, CLI, server-root, and repository name.
- Supplied admin UI is private fallback code. Users see the same page names and can create sparse same-name overrides, but never see/copy the base implementation.
- Models stay internal. UI, agents, APIs, MCP, logs, exports, and notifications receive policy-projected DTOs only.
- An agent is delegated from a staff member and can never exceed that person's current access.
- Every published API major is supported and declares safe MCP parity. Agents use the CompanyHQ MCP gateway, not raw database, filesystem, shell, Docker, secret, or GitHub access.
- Git version-controls the product root, each company root, and every independently deployable app. GitHub is first, but source-control providers are adapter-based.
- Optional services use explicit toggles. Core auth/users/forms/SEO stay supplied; email, Resend, ecommerce, R2, Merchant, and future extensions are optional.
- Agent providers are optional. None is installed or enabled for a company or project until an owner explicitly adds and scopes it.

## Documentation map

| Area | Contract |
| --- | --- |
| Confirmed choices and unresolved implementation decisions | [decisions](decisions.md) |
| System boundaries and lifecycle | [architecture overview](architecture/overview.md) |
| Routes, list/detail pages, and MCP parity | [routing](architecture/routing.md) |
| API/DTO/MCP compatibility | [API versioning](architecture/api-versioning.md) |
| Server bootstrap and Docker operation | [provisioning](architecture/provisioning.md) |
| Admin shell | [admin overview](admin/overview.md) |
| CMS capabilities and content flow | [CMS overview](cms/overview.md) |
| Content types, entries, drafts, publishing, and revisions | [content lifecycle](cms/content-lifecycle.md) |
| Forms and submissions | [forms](cms/forms.md) |
| SEO fields and rendering contract | [SEO](cms/seo.md) |
| Media lifecycle | [media](cms/media.md) |
| Sparse overrides, same-name files, and detail variants | [Explorer overrides](admin/explorer-overrides.md) |
| Hover/select/comment/coding-agent handoff | [visual agent handoff](admin/visual-agent-handoff.md) |
| Identity and sessions | [authentication](auth/overview.md) |
| Authentication operations | [auth operations](auth/operations.md) |
| Browser cookies and CSRF | [cookie strategy](auth/cookies.md) |
| JWT clients | [JWT strategy](auth/jwt.md) |
| Token claims | [token data](auth/token-data.md) |
| Service credentials | [API keys](auth/api-keys.md) |
| External identity adapters | [custom strategies](auth/custom-strategies.md) |
| Verification and recovery messages | [auth email](auth/email.md) |
| Roles, policy, field exposure, and delegation | [access control](auth/access-control.md) |
| Internal models and returned DTOs | [models and DTOs](models-and-dtos.md) |
| Agent runtime and modular adapters | [agents](agents/overview.md) |
| Agent roles, profiles, skills, and tools | [agent profiles and skills](agents/profiles-and-skills.md) |
| Multi-agent task graphs and dispatch | [agent orchestration](agents/orchestration.md) |
| MCP gateway | [MCP contract](agents/mcp.md) |
| Optional Hermes provider | [Hermes adapter](agents/hermes-sync.md) |
| Built-in and optional services | [services](services/overview.md) |
| Custom service creation | [custom service](services/custom-service.md) |
| Provider-neutral email and Resend | [email](services/email.md) and [Resend adapter](services/email-resend.md) |
| Commerce | [ecommerce](services/ecommerce.md) |
| Company media boundary | [Cloudflare R2](services/cloudflare-r2.md) |
| Catalogue distribution | [Google Merchant](services/google-merchant.md) |
| GitHub, worktrees, PRs, version control, and backups | [source control](source-control.md) |
| Persistent data and migrations | [database](database.md) |
| Events and integrations | [hooks](hooks.md) |
| Tasks, agent work, and marketing | [pipeline](pipeline.md) |
| Desktop/mobile notices | [notifications](notifications.md) |
| Security baseline | [security](security.md) |
| Test requirements and completion evidence | [testing](testing.md) |

## Required agent skill

The repository-local [CompanyHQ extension-governance skill](../.agents/skills/companyhq-extension-governance/SKILL.md) is mandatory when an agent changes an admin page, detail variant, override, service, model/DTO, project surface, route/API, MCP contract, or related security/test behavior.
