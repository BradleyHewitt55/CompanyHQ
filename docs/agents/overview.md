# Agents

CompanyHQ is agent-driven, not agent-owned. Agents are optional provider runtimes that execute a delegated task through CompanyHQ policy, DTO, source-control, test, approval, and audit boundaries.

## Default state

No agent provider is installed or enabled by default. The provider catalogue may show supported providers and detected command-line tools, but detection grants no access and starts no process.

An owner must add a provider, choose company or project scope, configure its runtime and credential references, create at least one role-bound profile, review its skills/tools/data/network access, and enable it. Disablement blocks new runs and revokes active capabilities; removal follows retention and in-progress task checks.

## Modular agent layer

| Module | Responsibility |
| --- | --- |
| Provider catalogue | Shows available, detected, installed, enabled, disabled, unhealthy, and update-available providers. |
| Provider adapter | Starts/stops a configured runtime and translates its supported protocol. |
| Profile registry | Binds an agent role to skills, tools, services/projects, limits, and approval rules. |
| Delegation broker | Intersects the profile with the current staff principal and task scope. |
| Orchestrator | Decomposes approved goals, links dependent tasks, and routes only to eligible profiles. |
| Task adapter | Creates/links provider tasks and projects safe task status into the Pipeline. |
| Worktree manager | Creates permitted branches/worktrees and prevents protected-main writes. |
| MCP broker | Exposes only authorized, schema-validated tools and resources. |
| DTO projector | Supplies task-relevant schemas and data without raw models. |
| Audit/test gate | Records execution and enforces evidence and approvals. |

A provider adapter never introduces a separate authorization model. Every provider and command-line agent uses these modules.

## Provider enablement

The Admin Agents page and CLI expose the same governed operations:

~~~text
companyhq agent provider add -provider <provider-key> -scope <company|project> \
  -target <company-or-project-key> [-runtime <self-hosted|local-cli|remote>]
companyhq agent provider enable -profile <profile-key>
companyhq agent provider disable -profile <profile-key>
companyhq agent provider remove -profile <profile-key>
~~~

`add` creates a configuration Change; it does not silently enable the provider. `enable` requires healthy runtime checks, role/profile review, credential references, test evidence, and approval. Command-line providers run through a registered adapter with an explicit executable identity, working-directory policy, environment allowlist, and capability bridge. They never inherit the server's unrestricted shell environment.

## Delegated access

An agent run receives the intersection of the delegating staff member's current access, the enabled profile, assigned agent role, active skills/tools, company/project/service allowlists, exact task, Change, worktree, action, approval, time, cost, and network limits. It is rechecked before every action.

If a user loses access, a provider/profile/skill is disabled, a task is cancelled, a service becomes unavailable, or an approval expires, the capability is revoked and the run stops or returns to review.

## Isolation

Each run executes in a company-scoped worker with only its assigned worktree and task inputs. It receives no protected-main write, supplied base source, another worktree/company, raw database, host Docker control, unrestricted filesystem/shell/network, or provider credentials. Service access occurs through policy-aware APIs or MCP tools and policy-projected DTOs.

## Task flow

~~~text
assignment -> scope calculation -> role/profile and skill resolution
-> plan or dependency graph -> isolated execution -> evidence and result
-> in review -> reviewer decision -> done -> separate merge/deploy approval
~~~

Pipeline Done means the requested agent task is complete. It does not mean a pull request was merged or production changed.

See [profiles and skills](profiles-and-skills.md), [task orchestration](orchestration.md), [MCP](mcp.md), [Hermes adapter](hermes-sync.md), and [Pipeline](../pipeline.md).
