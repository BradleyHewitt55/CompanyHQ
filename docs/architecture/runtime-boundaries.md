# Runtime and package boundaries

> **Document status:** Target V1 architecture contract.

Dependencies point inward: transports → application services → domain policy/contracts → ports. Adapters implement ports at the edge.

| Package group | May import | Prohibited imports/actions |
| --- | --- | --- |
| contracts/domain | Zod and pure utilities | Frameworks, SQL, Redis, network, filesystem |
| application services | contracts, domain ports, authorization | Next.js UI, provider SDK internals, raw SQL clients |
| database adapters | contracts, domain ports, PostgreSQL driver | UI, agent provider SDKs, external HTTP |
| web/admin | contracts and application client ports | Database adapters, Redis clients, provider processes |
| agent adapters | provider SDK, agent ports, telemetry | Database adapter, admin internals, arbitrary tools |
| MCP/tools | tool contracts and command client | SQL, unrestricted shell/filesystem/network/secrets |
| workers | queue contracts and application services | Authorization bypass, request-only session state |
| extensions | public extension SDK only | private package paths, adapter internals |

Architecture tests must fail forbidden imports. Database connectivity is available only to `packages/database` and application composition roots. External calls require registered adapters with host allowlists, timeouts, redaction, circuit breaking, and audit classification.

All package public APIs use explicit export maps. No package may import another package's `src/` path. Circular dependency detection and API-extractor/type tests run in CI.
