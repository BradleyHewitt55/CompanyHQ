# Agent task orchestration

The orchestration service turns an approved goal into bounded tasks and routes each task to an enabled profile whose role, skills, tools, scope, and limits satisfy the work.

## Task graph

Each task has a stable ID, parent goal, requester/delegator, company/project/service, acceptance criteria, assigned profile, required skills, allowed tools/DTOs/source scope, dependencies, Change/worktree when applicable, approval class, budget/time limits, status, evidence, result, and audit history.

Dependencies form an acyclic graph. A task becomes runnable only when required predecessors have an accepted result. Failed, blocked, cancelled, or superseded dependencies prevent automatic downstream execution.

## Orchestrator boundary

An orchestrator may inspect eligible profile summaries, decompose work, create child-task proposals, link dependencies, and recommend assignments. It cannot perform implementation work with hidden tools, expand the user's scope, enable providers/skills, approve its own graph, or route a task to a profile whose effective access is insufficient.

Material changes to external effect, ownership, security, deletion, publication, financial action, deployment, or approval remain user decisions. The graph records unresolved decisions rather than inventing them.

## Dispatch

~~~text
approved task -> dependency check -> profile health and capacity
-> effective-access calculation -> skill/tool resolution
-> isolated run claim -> heartbeats and safe progress
-> result/evidence -> review -> completion or revision
~~~

Claims have a unique run ID, lease, heartbeat, retry count, and idempotency key. Lease expiry does not assume the work failed safely; the dispatcher first reconciles provider state and worktree evidence. Duplicate completion and stale-run updates are rejected.

## Failure and review

Bounded retries apply only to declared retryable failures. Authentication, authorization, approval, configuration, repeated identical failures, unsafe output, and ambiguous external effects route to review instead of retrying blindly.

Reviewers receive acceptance criteria, effective access, active skills/tools, safe activity summary, changed files or content revisions, tests, preview, artifacts, provider task link, cost/time summary, and unresolved warnings. A reviewer can approve, request revision, reject, cancel, or reassign within policy. Agents cannot self-approve protected results.

## Provider adapters

Provider-native queues, sessions, subtasks, logs, and states remain behind the adapter. CompanyHQ is authoritative for delegation, access, approval, Change/worktree, review, and the owner-facing task record. The adapter reconciles durable identifiers, supports cancellation and revocation, and projects only the safe fields defined by [Pipeline](../pipeline.md).
