# Architecture and workflow diagrams

> **Document status:** Target V1 visual contracts.

## Company installation architecture

```mermaid
flowchart TB
  D["Company domain"] --> W["Next.js public + /admin"]
  W --> S["Application services"]
  M["Installation MCP"] --> S
  K["BullMQ workers/scheduler"] --> S
  S --> P[("Company PostgreSQL")]
  S --> R[("Company Redis")]
  S --> O["Company storage"]
  K --> A["Agent adapters + task worktrees"]
```

## Domain and admin routing

```mermaid
flowchart LR
  H["HTTPS company domain"] --> X{"Path"}
  X -->|"/admin/*"| AD["Authenticated admin registry"]
  X -->|"/api/*"| API["Versioned API transport"]
  X -->|"/mcp"| MCP["Authenticated MCP transport"]
  X -->|"other"| PUB["Published public route resolver"]
```

## Company directory layout

```mermaid
flowchart TB
  R["CompanyHQ/CompanyName"] --> A["admin/"]
  R --> P["app/"]
  R --> D["docker-compose/"]
  R --> E["extensions/"]
  R --> J["projects/"]
  R --> S["storage/"]
  R --> T["tasks/"]
  R --> W["worktrees/"]
  R --> V[".env"]
```

## Provisioning

```mermaid
flowchart LR
  C["CLI + verified SSH"] --> P["Preflight/interview"] --> J["Installation journal"] --> L["Create layout/secrets"] --> D["Start Compose"] --> M["Migrate/bootstrap owner"] --> H["Health/handoff"]
  H -. failure .-> J
```

## Authentication and permissions

```mermaid
flowchart LR
  I["Credentials/session/capability"] --> A["Authenticate"] --> C["Company + actor context"] --> P["RBAC grants ∩ ABAC constraints"] --> O{"Obligations"}
  O -->|"none"| X["Execute"]
  O -->|"step-up/approval"| H["Human decision"] --> X
```

## CMS schema flow

```mermaid
flowchart LR
  C["TypeScript config"] --> E["Ordered extension transforms"] --> V["Normalize + validate"] --> R["Immutable runtime registry"]
  R --> S["Server operations"]
  R --> A["Serializable admin manifest"]
  R --> T["Generated types"]
```

## DTO projection

```mermaid
flowchart LR
  Q["Authorized query predicate"] --> D["Domain records"] --> P["Policy + DTO projector"] --> O["Bounded surface DTO"]
  P -->|"hidden/denied"| X["Omitted"]
```

## Admin override fallback

```mermaid
flowchart LR
  S["Registered slot"] --> C{"Compatible company override?"}
  C -->|"yes"| O["Render override with public props"]
  C -->|"no"| B["Render supplied fallback"]
  C -->|"unsafe contract"| F["Block deployment"]
```

## Content mutation

```mermaid
sequenceDiagram
  participant U as Human/Agent
  participant C as Command service
  participant P as Policy/workflow
  participant D as PostgreSQL
  participant O as Outbox
  U->>C: Typed command + expected version + idempotency
  C->>P: Authorize/validate/approval
  C->>D: Mutation + revision + audit
  C->>O: Events in same transaction
  C-->>U: Projected result DTO
```

## Draft and publishing flow

```mermaid
stateDiagram-v2
  [*] --> Draft
  Draft --> InReview
  InReview --> ChangesRequested
  ChangesRequested --> Draft
  InReview --> Approved
  Approved --> Published
  Published --> Unpublished
  Unpublished --> Draft
  Published --> Draft: restore as new revision
```

## Brain dump to task

```mermaid
flowchart LR
  B["Untrusted brain dump"] --> P["Agent structured proposal"] --> H["Human edits/confirms"] --> T["Durable task + criteria/scope"] --> Q["Dependency/policy checks"] --> R["Runnable"]
```

## BullMQ execution

```mermaid
flowchart LR
  O["PostgreSQL outbox/task"] --> P["Idempotent publisher"] --> Q["Installation BullMQ queue"] --> W["Worker claim + DB lease"] --> C["Application command"] --> R["Receipt/result"]
  W -. heartbeat/retry .-> Q
  Q --> D["DLQ + incident"]
```

## Agent connection

```mermaid
flowchart LR
  U["Owner enables provider/profile"] --> B["Connection broker"] --> A["Provider adapter"] --> P["External provider"]
  A --> E["Canonical events"] --> O["Orchestration"]
  O --> M["Scoped MCP capability"]
```

## Agent Chat

```mermaid
flowchart TB
  UI["Admin chat"] <--> ST["Durable thread/activity stream"]
  ST <--> OR["Task/run orchestrator"]
  OR <--> AD["Provider adapter"]
  AD --> MC["MCP tools"]
  OR --> AP["Approval/user-input requests"] --> UI
```

## Hermes profiles

```mermaid
flowchart LR
  H["Hermes local or remote"] --> A["Hermes adapter"] --> P["CompanyHQ profile ∩ task capability"] --> M["Installation MCP"]
  H -. "native memory/tools are not authority" .-> X["Blocked direct effects"]
```

## MCP authentication

```mermaid
sequenceDiagram
  participant A as Agent adapter
  participant I as Capability issuer
  participant M as MCP server
  A->>I: Enabled profile + live task/run
  I-->>A: Short-lived audience-bound token
  A->>M: Connect/call + token
  M->>M: Verify key, audience, expiry, nonce, revocation
```

## MCP authorization

```mermaid
flowchart LR
  C["MCP call context"] --> I["Delegator ∩ agent/profile"] --> T["Task/run ∩ tool/resource"] --> R["Company/project/worktree/record"] --> A["Approval/time/budget"] --> D{"Allow?"}
```

## MCP tool execution

```mermaid
sequenceDiagram
  participant A as Agent
  participant M as MCP
  participant P as Policy
  participant S as Application service
  A->>M: Tool + Zod input + idempotency
  M->>P: Reauthorize current call
  P-->>M: Constrained grant/obligation
  M->>S: Typed query/command
  S-->>M: Policy-projected DTO
  M-->>A: Bounded/redacted result
```

## Project source control

```mermaid
flowchart LR
  T["Approved task"] --> W["Task worktree"] --> V["VCS driver: status/diff/checkpoint"] --> H["Hosted provider: PR/checks"]
  W --> R["Human review"] --> C["Separately approved commit/push/PR"]
  C -.-> X["Human-only merge/deploy"]
```

## Worktree lifecycle

```mermaid
stateDiagram-v2
  [*] --> Planned
  Planned --> Active: create from approved base
  Active --> Leased: run claim
  Leased --> Active: release/checkpoint
  Active --> Review
  Review --> Active: changes requested
  Review --> Retained: task terminal
  Retained --> Cleaned: verified retention cleanup
  Active --> Quarantined: uncertain/orphaned
```

## Agent recovery

```mermaid
flowchart LR
  F["Crash/disconnect"] --> R["Reconcile DB lease + BullMQ + provider + worktree"] --> U{"Outcome known?"}
  U -->|"yes, compatible"| S["Resume/checkpoint"]
  U -->|"no"| Q["Quarantine/wait for human"]
  S --> N["New fenced attempt"]
```

## Audit flow

```mermaid
flowchart LR
  C["Authorized command"] --> T["PostgreSQL transaction"]
  T --> M["Business mutation"]
  T --> R["Revision"]
  T --> A["Append-only audit"]
  T --> O["Outbox"]
  O --> X["Jobs/integrations"]
```

## Development and production separation

```mermaid
flowchart TB
  C["Same contracts/images/topology"] --> D["Development: local secrets, fixtures, mock providers"]
  C --> P["Production: pinned images, TLS, external backups, restricted egress"]
  D -. "no data/credentials promotion" .- P
```
