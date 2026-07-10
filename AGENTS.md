# CompanyHQ agent rules

These rules apply to every human-guided or autonomous change in this repository.

## Protect reference repositories

- Treat everything under `.repos/` as read-only reference material.
- Never create, edit, format, move, delete, commit, or run a write-producing command in `.repos/` unless the user explicitly asks for that exact reference repository to be changed.
- Product documentation must stand on its own. Do not name or link reference repositories as implementation sources.

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

- Use CompanyHQ as the only product, tenant, repository, and runtime name.
- Never expose supplied base-admin source to tenant users or agents. Create a sparse, same-name tenant override only when a user intentionally edits a page; deleting it must restore the base UI.
- Return policy-projected DTOs, never raw models, to client UI, admin UI, agents, MCP tools, logs, or errors.
- Treat an agent as a delegated technical identity. Its effective access must never exceed the delegating staff member's current access and must be narrowed by task, service, Change, worktree, and approval scope.
- Never grant raw database, host shell, Docker socket, unrestricted filesystem, plaintext secret, or unrestricted source-control access.
- Never bypass authorization for internal, local, MCP, background-job, hook, or test calls.

## Source control and tests

- Work on a branch and isolated worktree, never protected main. Use the documented worktree name convention.
- Agent changes require a Change, diff, appropriate tests, review evidence, and a governed GitHub pull request before main can change.
- Choose tests by task impact: API/policy/DTO changes require API and security tests; UI changes require UI tests; combined work requires both; every service change requires its declared test evidence.
- Do not claim a task is complete until its required tests, docs, access checks, and approval state are recorded and tested.
