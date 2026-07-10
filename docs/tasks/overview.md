# Tasks

> **Document status:** Target V1 architecture contract.

Tasks are durable human/agent work records, not provider sessions. A task owns objective, acceptance criteria, requester/delegator, project/resource scope, priority, dependencies, assigned human/agent profile, worktree, approval class, budget, status, evidence, and audit history.

States are `draft`, `ready`, `queued`, `claimed`, `running`, `waiting_input`, `waiting_approval`, `blocked`, `in_review`, `changes_requested`, `completed`, `failed`, `cancelled`, and `superseded`. Every transition is a typed command with policy and optimistic version checks.
