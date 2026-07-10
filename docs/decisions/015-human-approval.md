# ADR-015: Bound human approval

> **Status:** Accepted. **Review:** 2027-01-15.

## Context and decision
High-risk content, source, policy, financial, and production actions need human control. Approval binds exact actor/action/resource revision/diff/policy and expiry; agents cannot approve their own work.

## Alternatives considered
Blanket session approval; role-only approval; provider confirmation prompts.

## Advantages, disadvantages, and risks
Decisions remain meaningful and auditable. Friction can be high; risk classes and batch approvals for identical bounded operations reduce noise without broad grants.

## Consequences and migration
Any relevant change invalidates approval. Merge and deployment remain separate human actions.
