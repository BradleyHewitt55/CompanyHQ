# ADR-013: MCP mandatory in V1

> **Status:** Accepted. **Review:** 2027-01-15.

## Context and decision
Agents need a stable, governed tool/resource/prompt boundary. Every installation ships an MCP server and comprehensive contracts.

## Alternatives considered
Provider-native tools only; REST only; direct service/database access.

## Advantages, disadvantages, and risks
One audited capability surface reduces adapter sprawl. MCP exposure increases attack surface; per-call authorization, strict schemas, limited tools, and isolation tests are mandatory.

## Consequences and migration
No direct database tools. Local connections are authenticated. Tool additions require security and audit declarations.
