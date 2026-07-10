# Authorization architecture

> **Document status:** Target V1 implementation contract.

Authorization inputs are typed `Principal`, `CompanyContext`, `Action`, `ResourceDescriptor`, and request attributes. Policies compile at startup into an evaluator with stable reason codes. Collection configuration may reference named policy functions but cannot embed arbitrary client-visible logic.

Repositories accept only `AuthorizedQuery` objects emitted by application query services. Commands pass an `AuthorizationGrant` into the unit of work, where field mutations and workflow obligations are rechecked against current state. Approval is an obligation, not an allow result.

Privileged owner operations require recent MFA/step-up and may require a second human. System jobs use dedicated service principals constrained to one command family. Migrations use a separately audited deployment identity and cannot be called from agent tools.

Tests cover policy matrices, query predicate injection, field omission, relationship traversal, stale approvals, revoked users/capabilities, confused-deputy calls, and identical behavior across UI/API/MCP/worker transports.
