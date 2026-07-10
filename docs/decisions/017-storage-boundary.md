# ADR-017: Installation-scoped storage adapters

> **Status:** Accepted. **Review:** 2027-01-15.

## Context and decision
Media and artifacts require local or object storage without cross-company paths. Use a CompanyHQ storage port whose adapter is configured per installation with mandatory prefix/bucket and private-by-default access.

## Alternatives considered
Shared global bucket paths chosen by callers; database blobs; public filesystem.

## Advantages, disadvantages, and risks
Adapters support deployment choice while centralizing policy. Misconfiguration can still collide; provisioning uniqueness probes and object-key enforcement block startup.

## Consequences and migration
Public delivery uses signed or explicitly published variants. Agents receive artifact handles, not storage credentials.
