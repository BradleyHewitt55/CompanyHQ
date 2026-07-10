# CompanyHQ agent rules

These rules apply to every human-guided or autonomous change in this repository.

## Protect reference repositories

- Treat everything under `.repos/` as read-only reference material.
- Never create, edit, format, move, delete, commit, or run a write-producing command in `.repos/` unless the user explicitly asks for that exact reference repository to be changed.
- Product contracts must stand on their own. Reference repositories may be named only in `docs/reference-studies/`; never import or link their internals as CompanyHQ implementation dependencies.

## Follow the accepted architecture

- Start with `docs/README.md`, `docs/decisions/README.md`, and the relevant roadmap phase.
- CompanyHQ is TypeScript/Next.js/React with PostgreSQL, Redis, BullMQ, Zod contracts, and Docker Compose. Another language or core technology requires an ADR and owner approval.
- One installation belongs to one company. There is no central application control panel, company switcher, cross-company super-admin, shared database, or shared application state.
- Never read, query, copy, cache, enqueue, log, or expose another company's data, files, tasks, agents, repositories, MCP services, or secrets.
- Keep web/admin, application services, database adapters, workers, scheduler, MCP, agent adapters, source control, audit, and observability behind the boundaries in `docs/architecture/runtime-boundaries.md`.
- Configuration compiles into an immutable registry. Extensions use the public SDK and may not import private package internals.
- PostgreSQL is authoritative. Redis/BullMQ, provider sessions, caches, and filesystem artifacts are reconstructible coordination state.

## Treat documentation as part of the feature

- Read the relevant files under docs before planning a change.
- Update the relevant docs in the same Change whenever implementation, behavior, security, source ownership, route, API, DTO, service, agent capability, test approach, or product approach changes.
- A new service, page, override, route, MCP operation, model attribute, DTO, provider adapter, or workflow is incomplete until its documentation and agent-skill reference are updated.
- Update the CompanyHQ extension-governance skill when a governed naming, override, service, or agent workflow changes.

## Ask instead of guessing

- Ask the user for direction when a material requirement is unclear, contradictory, security-sensitive, destructive, externally visible, or changes ownership/approval behavior.
- Do not invent credentials, permission grants, deployment targets, retention rules, provider behavior, user data, or a product decision that the docs do not support.
- Record a clear open decision in the docs when work can safely continue without resolving it. Stop when it cannot.

## Preserve CompanyHQ safety boundaries

- Use CompanyHQ as the only product and installation name. Treat company and installation identity as an enforced security boundary on every request, job, cache key, file, event, agent run, tool call, and audit record.
- Never expose supplied base-admin source to tenant users or agents. Create a sparse, same-name tenant override only when a user intentionally edits a page; deleting it must restore the base UI.
- Return policy-projected DTOs, never raw models, to client UI, admin UI, agents, MCP tools, logs, or errors.
- Treat an agent as a delegated technical identity. Its effective access must never exceed the delegating staff member's current access and must be narrowed by task, service, Change, worktree, and approval scope.
- Never grant raw database, host shell, Docker socket, unrestricted filesystem, plaintext secret, or unrestricted source-control access.
- Never bypass authorization for internal, local, MCP, background-job, hook, or test calls.
- Agents may prepare changes in their assigned task worktree. They may never merge, approve, deploy, change protected branches, or act outside short-lived task capabilities.
- Every mutation uses a typed application command enforcing company context, authentication, authorization, validation, workflow/approval, optimistic concurrency, idempotency, immutable revision history, and audit.
- Agent/provider integrations use application services through the installation MCP boundary. Do not create a provider-specific privileged path.
- Never expose or log passwords, tokens, cookies, connection strings, provider credentials, secret values, or unrestricted prompts/tool output.

## Source control and tests

- Work on a branch and isolated worktree, never protected main. Use the documented worktree name convention.
- Use one worktree per parent task; source-changing subtasks share that worktree under its lease.
- Agent changes require a Change, diff, appropriate tests, review evidence, and a governed GitHub pull request before main can change.
- Never let an agent merge a branch or pull request, modify primary/protected branches directly, or deploy automatically.
- Choose tests by task impact: API/policy/DTO changes require API and security tests; UI changes require UI tests; combined work requires both; every service change requires its declared test evidence.
- Do not claim a task is complete until its required tests, docs, access checks, and approval state are recorded and tested.
- Record assumptions and unresolved decisions in the relevant status-labeled document. Do not copy reference implementations wholesale or couple CompanyHQ to their internals.
