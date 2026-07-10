# Hooks, events, and integrations

Hooks keep synchronous work small and predictable. Durable events and jobs handle slow, retried, or externally visible work. This prevents a content save, API request, or Explorer edit from accidentally becoming an agent deployment runner.

## Hook categories

| Category | Allowed responsibility |
| --- | --- |
| Before validation | Normalize input, validate invariants, enrich safe context, and deny invalid work. |
| Before commit | Enforce policy, record intended audit context, and prepare atomic local state. |
| After commit | Emit a durable domain event; do not run long work inline. |
| Read/presentation | Apply safe field filtering, computed presentation, and view context without mutating state. |
| Integration ingress | Verify source authenticity, parse safely, deduplicate, and enqueue an event/job. |

Hooks do not call uncontrolled external services, run migrations, deploy, send campaigns, invoke broad agents, or retry indefinitely. Those actions belong to jobs and workflows.

## Event contract

Events use a stable, versioned name:

~~~text
<domain>.<entity>.<event>.v<version>
~~~

An event includes company, actor, correlation identifier, source Change or task when present, safe data reference, timestamp, and idempotency key. Consumers must tolerate duplicate delivery and must record their outcome.

Examples of domains include content, service, project, change, deployment, ecommerce, email, media, pipeline, agent, model, dto, and notification.

## Jobs and workflows

Long-running work such as an agent run, build, preview, deployment, database migration, email campaign, Merchant feed, media processing, notification delivery, or backup runs in a durable job/workflow with:

- company, service/project, and actor context;
- idempotency key;
- bounded concurrency;
- retry and backoff policy;
- timeout and cancellation behavior;
- dead-letter or manual remediation route;
- observable progress and final audit event.

Jobs repeat policy and DTO checks at execution time. A queued job cannot continue merely because the requester once had access. An agent-triggered job must also verify the delegated staff user, task, Change, worktree, service/project, and approval state.

## Inbound webhooks

Provider callbacks enter only through registered integration routes. The receiver verifies the signature using a secret reference, stores minimal safe event evidence, uses the provider event ID or a calculated idempotency key, and then queues a job.

Untrusted provider data, especially inbound email or user-provided webhook content, cannot become instructions for an agent without a guarded workflow and human-approved policy.

## Service authoring rule

Every new hook, event, workflow, or webhook updates the service contract, API/MCP mapping, access policy, failure/retry behavior, observability, and backup/retention impact. An undocumented side effect blocks service approval.
