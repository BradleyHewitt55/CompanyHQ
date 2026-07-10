# Pipeline

> **Document status:** Target V1 product contract; see `tasks/` for durable execution details.

The Pipeline is the company work board for staff and delegated-agent tasks. It shows the information needed to understand ownership, progress, review, and outcome without exposing provider execution internals.

## Board flow

Unassigned ideas stay in a personal or team backlog. Once work starts, every active task uses the same board flow:

~~~text
In progress -> In review -> Done
~~~

| State | Meaning |
| --- | --- |
| In progress | The assigned person or agent is executing the accepted task or retrying declared recoverable work. |
| In review | A result, decision, approval, revision, failure, or security warning requires review. |
| Done | The requested task result was accepted as complete. Source merge and deployment remain separate Change states. |

Cancelled and Archived are terminal filters, not normal progress columns. Needs attention, Failed, Stopped, Offline, or Blocked are concise badges on In progress or In review cards; they do not expose a provider's private state machine.

## Agent task card

An agent-linked card may show only:

- task title and acceptance summary;
- requester/delegator and assigned CompanyHQ profile/role;
- company, project/service, and linked Change when present;
- In progress, In review, or Done;
- safe latest progress or result summary and attention badge;
- active CompanyHQ skill names and versions;
- test/preview/review evidence and safe artifact references;
- runtime/cost summary when enabled;
- last update and an authorized provider-task detail link.

The Pipeline does not show raw prompts, chain-of-thought, provider-owned skills, unrestricted logs, secrets, environment data, hidden DTO fields, internal provider subtasks, or another user's context.

## Provider synchronization

Every provider adapter maps its internal state to the three CompanyHQ states. CompanyHQ owns assignment, delegation, effective access, review, notifications, Change/worktree linkage, and accepted completion. Providers own their internal execution.

Hermes-linked tasks follow the [Hermes adapter contract](agents/hermes-sync.md). Command-line and future providers follow the same projection instead of adding provider-specific columns.

## Review and source state

An authorized reviewer can request revision, approve the task result, reject, stop, cancel, or reassign within policy. The agent cannot approve its own protected work.

For source tasks, the card links the worktree, diff, tests, preview, and pull request. Pull-request review, merge, deployment, and rollback have their own Change status. Marking the task Done never claims those actions occurred.

## Visibility and notifications

Owners see company work allowed by owner assignment. Staff see their tasks and records allowed by role/record policy. Agents see only the exact assigned task, acceptance criteria, active capability, allowed DTOs, and worktree scope.

Assignment, attention, review, completion, cancellation, and provider-offline events notify relevant users according to [notification preferences](notifications.md). Opening a card repeats authorization; a stale board item never grants access.
