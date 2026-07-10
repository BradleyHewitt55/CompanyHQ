# Hermes provider adapter

> **Document status:** Target V1 supporting detail; `agents/hermes.md` is the concise authoritative boundary.

Hermes is an optional agent provider. It is not installed, configured, started, or enabled in a new CompanyHQ instance. A company owner may add a self-hosted company-level instance and then enable one or more project/company profiles.

## Installation and enablement

The provider catalogue supports `self-hosted` Hermes instances. Adding one creates a governed configuration Change that declares runtime image/package version, company-scoped storage, endpoint/bind policy, credential references, health check, task protocol, profile isolation, skills/tools discovery, network policy, resource limits, backup/retention, update, disable, removal, and rollback.

The instance runs inside the company's server boundary and is unreachable from public networks by default. CompanyHQ connects through an authenticated private transport. If a remote endpoint is deliberately used, it requires TLS, exact endpoint allowlisting, credential rotation, and the same company isolation.

CompanyHQ does not import or install Hermes skills. Owners select CompanyHQ-managed skills for each role/profile. The adapter may read safe provider capability metadata to determine compatibility.

## Profile mapping

Each enabled CompanyHQ profile maps to one isolated Hermes profile. The mapping records provider instance, external profile identifier, CompanyHQ role, allowed task categories, services/projects, skills, tools, concurrency, cost/time limits, and health. One profile cannot see another profile's memory, sessions, workspace, credentials, or task context.

## Task link

Assigning a task to a Hermes profile creates or links one provider task using durable IDs and an idempotency key. CompanyHQ stores:

- CompanyHQ task and run IDs;
- provider task, profile, and session/run identifiers;
- guarded link to the provider task when an authorized UI is available;
- assigned role and profile;
- active CompanyHQ skill names and versions;
- safe progress/result summary, artifact references, review evidence, cost/time summary, and last synchronization time.

Raw prompts, chain-of-thought, unrestricted logs, secrets, environment values, broader workspace context, provider-owned skills, and internal subtask detail are not synchronized into the Pipeline.

## Pipeline status projection

The CompanyHQ UI projects a Hermes-linked task into exactly the normal agent task states:

~~~text
In progress -> In review -> Done
~~~

| CompanyHQ state | Provider signals |
| --- | --- |
| In progress | Task created/claimed, active execution, safe heartbeat, retryable work, or child work still running. |
| In review | Result submitted, review requested, approval required, blocked decision, non-retryable failure, or security warning needing a person. |
| Done | Accepted task result is complete. Merge and deployment remain separate Change states. |

Provider-native queued/running/blocked/retry/subtask states do not create extra Pipeline columns. The card may show a concise Needs attention, Failed, Stopped, or Offline badge while remaining In progress or In review. Provider internals are available only through an authorized detail link.

## Synchronization

- Use durable task/run identifiers, revision numbers, and idempotency keys; never match by title.
- Verify event authenticity and sequence; reconcile missed, duplicate, delayed, and out-of-order updates.
- Recheck the delegator, company, profile, task, service/project, skills/tools, Change/worktree, and approval before accepting an action or result.
- Propagate cancellation, access revocation, profile/skill disablement, and company shutdown immediately.
- Treat provider text, files, URLs, and tool output as untrusted data.
- Keep CompanyHQ authoritative for task status acceptance, review, source Change, and deployment approval.

Adapter enablement tests capability discovery, profile isolation, task create/link/cancel, active-skill mapping, state projection, retries/reconciliation, offline recovery, event authentication, revocation, DTO redaction, and self-hosted backup/restore.
