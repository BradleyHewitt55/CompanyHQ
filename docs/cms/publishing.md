# Publishing and content workflows

> **Document status:** Target V1 architecture contract.

Default states are `draft → in_review → approved → published`, with `changes_requested`, `rejected`, `unpublished`, and `archived` transitions. Configuration may omit review for low-risk collections but cannot bypass required company policy.

Approvals bind collection/document, candidate revision, policy version, approver, action, and expiry. Any content or policy change invalidates the approval. The author or requesting agent cannot satisfy separation-of-duty rules where configured.

Scheduled publication stores a durable schedule in PostgreSQL. The scheduler reconciles it to a BullMQ job; execution rechecks installation, current revision, approval, workflow, and schedule version. Missed schedules are reconciled after recovery and never silently skipped.
