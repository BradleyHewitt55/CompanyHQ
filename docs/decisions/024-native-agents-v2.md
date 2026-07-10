# ADR-024: Native CompanyHQ agents deferred to V2

> **Status:** Accepted V2 deferral. **Review:** after V1 provider adapters, MCP, and operations are production-proven.

## Context and decision
Building a proprietary model runtime would distract from safe CMS orchestration. V1 integrates external providers through adapters; a native CompanyHQ agent runtime is V2.

## Alternatives considered
Build native first; fork a reference runtime; omit agents.

## Consequences, risks, security, and migration
V1 depends on provider availability but preserves choice and original boundaries. Canonical task/event/tool contracts prevent lock-in. V2 must reuse policy/MCP, not create a privileged path.
