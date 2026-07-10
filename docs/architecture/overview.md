# Architecture overview

CompanyHQ is a control plane for a company, not merely a content editor. It coordinates people, services, projects, source changes, agents, and deployments while keeping each company isolated.

~~~text
Human staff / owner             Enabled provider agents
        |                                  |
        +---- Admin UI / CLI / Explorer ---+
                         |
                 CompanyHQ control plane
   policy | company registry | Change records | audit | MCP gateway
                         |
      +------------------+--------------------+
      |                  |                    |
Company service     project/app repo      agent runtime
and content data    worktrees + GitHub    isolated Docker workers
      |                  |                    |
database/media     preview/deploy           scoped MCP capabilities
      \____________________|___________________/
                           |
                  company Docker composition
~~~

## Core concepts

| Concept | Meaning |
| --- | --- |
| Company | The tenant boundary and operational unit. |
| Core service | A supplied capability that is always present, such as users, auth, forms, or SEO. It is configured and permissioned, not toggled off. |
| Optional service | An installable capability, such as Resend, ecommerce, Cloudflare R2, or Google Merchant. It has an explicit enabled state. |
| Project | A deployable website, mobile app, or desktop app added when needed. Projects are not created by default. |
| Change | The universal record for a manual or agent source, configuration, or deployment change. |
| Agent provider/run | An optional enabled runtime and a scoped execution attached to a task, policy identity, and, where source changes, a Change/worktree. |
| Explorer item | A view of a source-backed admin or project item, including its effective source and history. |

## Company boundary

Each company receives an isolated composition. It must have its own:

- server directory and environment file;
- Docker network and containers;
- database identity and credentials;
- media storage boundary;
- GitHub repositories and installation scope;
- agent worktrees, scoped capabilities, and secret references;
- audit, backup, and deployment records.

No agent, staff member, query, tool call, secret, or Explorer view may cross a company boundary without an explicit owner-level assignment and policy decision.

## Source of truth

The product root, each company root, and each deployable project use Git version control. Company root source contains only tenant-owned admin overrides/custom services; supplied base-admin source stays private in the product root. Content, tasks, audit events, service state, model/DTO registry, notifications, and deployment state live in operational storage. Secrets stay in a secret store or protected environment file and are represented in records only by references.

The Explorer shows only tenant-owned source on protected main by default, plus visual/DTO contracts for supplied pages. It must never claim that a runtime-only change is backed up in Git.

## Change lifecycle

All source-affecting work follows one lifecycle:

~~~text
draft -> plan -> isolated worktree -> preview and diff -> approval
      -> commit and push -> deploy -> audit and rollback point
~~~

An agent may prepare a draft and a worktree within its granted capability. The policy decides whether it may continue at each later transition. Long-running work is queued; it must not execute inside a request hook or an interactive UI action.
