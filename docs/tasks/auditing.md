# Task auditing

> **Document status:** Target V1 security contract.

Audit every task proposal, edit, assignment, claim, lease, heartbeat anomaly, run start/stop, provider session, prompt/context version, tool call, approval request/decision, file/content mutation, source-control action, retry, recovery, evidence, review, and terminal transition.

Payloads store classifications, hashes, summaries, and protected artifact references—not raw secrets or unrestricted prompts/tool output. The event chain includes installation, user, agent, session, task, run, tool, resource, timestamp, correlation, causation, policy, and idempotency IDs.
