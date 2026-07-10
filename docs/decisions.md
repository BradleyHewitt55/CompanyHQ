# Decisions and open questions

These decisions define the current CompanyHQ product contract.

## Confirmed decisions

| ID | Decision | Reason |
| --- | --- | --- |
| D-01 | CompanyHQ is the only product, tenant, server-root, CLI, and repository name. | One canonical name keeps provisioning paths, Docker names, repositories, secrets, backups, links, and user expectations consistent. |
| D-02 | Keep the product root repository, create one CompanyHQ root repository per company, and one repository per independently deployable app. | Tenant admin changes stay isolated and every app has independent release/rollback boundaries. |
| D-03 | GitHub is first; source-control providers are adapter-based. | Avoid a GitHub-only product contract while delivering a focused first integration. |
| D-04 | The Explorer opens main and exposes only tenant-owned source. Supplied base UI is private fallback code. | Users can override safely without receiving a brittle fork or leaking base code. |
| D-05 | Override files use the same page name and a (slug) detail variant. | The page remains recognisable while list/detail source and DTO contracts stay precise. |
| D-06 | Models are internal; every admin/client/agent/API/MCP response is a role-projected DTO. | This prevents field leaks and lets users safely add attributes by surface. |
| D-07 | An agent is delegated technical identity, never an independent authority role. | Its access is the intersection of the staff member, profile/service, task/Change/worktree, and approval scope. |
| D-08 | Optional services use toggles; auth/users/forms/SEO remain core. Email and ecommerce are optional, and the email provider is replaceable. | Companies should not run integrations they do not use. |
| D-09 | Agents work in named worktrees, use GitHub pull requests, and cannot write protected main. | Review, test evidence, rollback, and accountability stay intact. |
| D-10 | CompanyHQ has no API-version deprecation or removal lifecycle. | Published majors stay supported; breaking changes create a new major while security corrections remain enforceable on every major. |
| D-11 | Agent providers, including Hermes and command-line providers, are disabled by default and enabled per company or project. | Agent access must be deliberate, reversible, role-scoped, and visible. |

## Open product decisions

1. Set the first deployment target, public-domain model, database/media backup destination, and retention.
2. Set action-specific approval policy for commits, pushes, previews, production deploys, migrations, sending email, financial actions, and deletion.
3. Choose an ecommerce payment provider and whether Merchant publication can be automated after approval.
