# ADR-014: Intersection-based agent capabilities

> **Status:** Accepted. **Review:** 2027-01-15.

## Context and decision
Agents must never gain authority from profile composition. Effective access is the intersection of delegator, role/profile, task/run, resource/worktree, tool/action, approval, time, budget, and network limits.

## Alternatives considered
Standalone agent roles; provider permission modes; long-lived API keys.

## Advantages, disadvantages, and risks
Delegation stays attributable and revocable. Frequent checks cost latency; short cache windows with revocation/version validation are acceptable.

## Consequences and migration
Capabilities are short-lived and audience-bound. Provider sandbox modes can narrow, never broaden, access.
