# Drafts, versions, and rollback

> **Document status:** Target V1 architecture contract.

Every document mutation creates an immutable revision recording document ID, schema/config revision, content snapshot or canonical delta, workflow state, author/delegator/agent identities, command, expected version, reason, timestamps, and correlation IDs. The mutable document row points to current draft and published revision IDs.

Autosave creates rate-limited draft revisions with an idempotency key and expected version. Restore never edits history: it creates a new draft from the selected revision. Publishing atomically advances the published pointer after policy/approval checks and emits outbox events. Rollback is the same governed restore-and-publish flow.

Revision reads repeat current access and field projection; historical access is not a bypass. Secret fields are references or encrypted values and follow separate redaction/rotation rules.
