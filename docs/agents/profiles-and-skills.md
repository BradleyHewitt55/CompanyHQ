# Agent profiles, roles, skills, and tools

Every enabled agent instance uses a profile. Profiles are company- or project-scoped and represent a job function, not a provider-wide superuser.

## Profile contract

A profile declares:

- stable profile key, display name, provider instance, runtime version, and company/project scope;
- assigned agent role and permitted task categories;
- service, project, model/DTO, source path, network, and integration allowlists;
- installed skills and whether each is enabled, disabled, required, or task-selectable;
- tool and MCP allowlists, input/output limits, and approval class;
- concurrency, runtime, retry, budget, scheduling, and retention limits;
- worktree policy, test requirements, reviewer class, and notification rules.

Profiles start disabled. Cloning a profile copies declarations but not credential values, approvals, active tasks, or authority.

## Agent roles

| Role | Purpose | Typical limits |
| --- | --- | --- |
| Orchestrator | Breaks a goal into linked tasks and assigns eligible profiles. | No implementation shell/source tools; cannot approve child work. |
| CMS editor | Drafts and updates allowed content and SEO fields. | No schema, publication, contact export, or deployment unless separately granted. |
| CMS architect | Proposes content models, DTOs, migrations, and CMS docs/tests. | Changes require review and cannot expose fields beyond the delegator. |
| Developer | Prepares source Changes in an assigned worktree. | No protected-main write, secrets, production promotion, or self-approval. |
| Reviewer | Examines safe evidence against acceptance criteria. | Cannot review its own run or expand task scope. |
| Marketing operator | Prepares approved campaigns and content plans. | No real send/publication without the exact service approval. |
| Analyst | Reads permitted DTOs and produces reports. | No source or data mutation. |

Companies may define narrower roles. A role is a maximum profile boundary and never overrides the delegating user's current policy.

## Skills

Skills are versioned instruction packages with a name, description, source/provenance, content hash, supported roles/providers, required tools, required secret/config references, platform constraints, and risk classification. CompanyHQ does not copy skills from a provider runtime into the company catalogue.

Skills are installed deliberately at company or project scope and disabled until assigned to a profile. A task can select only enabled skills allowed by both its profile and delegator. Required skills load before execution; optional skills load on demand. Skill text is untrusted configuration and cannot grant tools, data, network, secrets, or approval.

Updates show a diff, changed permissions/config requirements, affected profiles, tests, and rollback. Modified company skills are never overwritten by provider updates. Disabling a skill prevents new use and revokes or pauses active runs that require it.

## Active-skill resolution

~~~text
installed for company/project
intersection enabled for profile
intersection compatible with role/provider/platform
intersection allowed for task and delegator
intersection healthy required tools/config
= active skills for this run
~~~

The run record stores skill name, version/content hash, activation reason, and safe outcome. The Pipeline may show the active skill names and versions, but not hidden instructions, secrets, or provider-internal context.

## Tools and access

Tools are authorized independently of skills. A skill asking for a disabled tool does not enable it. Discovery shows only tools and resources available to the current run; execution rechecks the user, role, profile, task, Change, worktree, service/project, DTO, approval, expiry, and request constraints.

The Agents UI shows effective access before enablement and on every run: delegator, role, scope, services/projects, writable source paths, DTOs, skills, tools, network destinations, approvals, limits, and expiry.
