# Threat model

> **Document status:** Confirmed Target V1 security baseline.

Protected assets are company content/PII, credentials, source, worktrees, media, approvals, audit, provider data, backups, and availability. Trust boundaries are browser/API, remote/local MCP, provider process, worker/queue, PostgreSQL, Redis, storage, source-control host, extension, webhook, CLI/SSH, and backup target.

Priority threats are cross-company leakage; authorization bypass; agent/tool privilege escalation; prompt injection and exfiltration; SQL/path/command/SSRF injection; stale approval/capability; direct DB writes; duplicate/ambiguous jobs; lost updates; malicious extensions; secret/log leakage; protected-branch changes; backup compromise; and audit tampering.

Controls are independent installations, central context/policy, typed application commands, least-privilege capabilities, sandbox/egress/path controls, immutable revisions/audit, optimistic concurrency, idempotency/reconciliation, separation of duties, signed webhooks/jobs, encryption, redaction, and adversarial tests.
