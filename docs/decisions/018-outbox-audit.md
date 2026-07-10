# ADR-018: Transactional outbox and immutable audit

> **Status:** Accepted. **Review:** 2027-01-15.

## Context and decision
Mutations, events, jobs, revisions, and audit must not diverge. Persist domain change, revision, audit, and outbox records in one PostgreSQL transaction; publish idempotently afterward.

## Alternatives considered
Publish directly inside requests; application logs as audit; Redis events as truth.

## Advantages, disadvantages, and risks
Crash consistency improves. Outbox backlog adds operations; lag alerts, batching, retention, and replay tools address it.

## Consequences and migration
External effects are never transaction hooks. Mutation commands fail if required audit cannot commit.
