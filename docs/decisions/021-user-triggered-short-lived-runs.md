# ADR-021: User-triggered short-lived runs

> **Status:** Accepted. **Review:** when unattended autonomy is proposed or by 2027-01-15.

## Context and decision
V1 must control agent authority and cost. Runs are triggered by an authorized user/task/schedule, receive short-lived task capabilities, and terminate or checkpoint when the task step completes, is cancelled, loses its lease, or exceeds budget.

## Alternatives considered
Always-on autonomous agents; provider-owned background sessions; long-lived credentials.

## Consequences, risks, security, and migration
Runs remain attributable and revocable but startup overhead rises. BullMQ and resumable adapter sessions reduce that cost. Unattended schedules still mint one bounded run per occurrence. Always-on native agents require a V2 ADR and new threat model.
