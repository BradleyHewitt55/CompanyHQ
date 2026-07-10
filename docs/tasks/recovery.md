# Task and run recovery

> **Document status:** Target V1 operational contract.

On startup, a reconciler scans nonterminal tasks/runs, BullMQ state, PostgreSQL leases, provider sessions, worktrees, approvals, and external request receipts. Expired leases are not blindly retried: uncertain side effects are reconciled first.

Runs resume from durable checkpoints when the adapter proves compatible; otherwise a new attempt receives the safe task summary and worktree state. Stale attempts cannot publish progress or completion. Orphaned worktrees are quarantined, never automatically deleted.
