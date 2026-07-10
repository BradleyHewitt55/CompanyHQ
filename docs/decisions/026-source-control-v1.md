# ADR-026: Source control is a V1 feature

> **Status:** Accepted. **Review:** after the Git/GitHub implementation spike or by 2027-01-15.

## Context and decision
Coding-agent work cannot be reviewed or recovered safely without repository identity, status, diffs, task branches/worktrees, checkpoints, and change-request integration. Git plus a GitHub hosted-provider adapter are V1; adapter contracts preserve later VCS/host choices.

## Alternatives considered
Defer source control to V2; shell out directly from agents; GitHub-only contracts; shared mutable checkout.

## Consequences, risks, security, and migration
V1 scope and process attack surface increase. Fixed argument arrays, canonical task worktrees, bounded output/time, credential brokers, protected-ref policy, separate commit/push/PR approvals, and human-only merge/deploy are mandatory. Other providers must pass the same capability/security suite rather than leak into domain contracts.
