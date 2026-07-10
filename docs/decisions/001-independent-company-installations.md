# ADR-001: Independent company installations

> **Status:** Accepted. **Review:** 2027-01-15.

## Context and decision
Cross-company leakage is the dominant risk. Deploy one CompanyHQ stack per company with independent database, Redis, storage, keys, queues, MCP, projects, and audit; provide no central switcher/super-admin.

## Alternatives considered
Shared database row tenancy; schema-per-company; central control plane coordinating isolated data planes.

## Advantages, disadvantages, and risks
Strong blast-radius and operational ownership boundaries outweigh higher infrastructure/update cost. Drift between installations is the main risk, controlled by pinned releases, health inventory owned by the operator, and idempotent upgrades.

## Consequences and migration
All contracts carry installation identity even inside a dedicated stack. Any prior central-control-plane language is removed. Shared SaaS hosting would require a new ADR and threat model.
