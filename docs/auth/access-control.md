# Access control

CompanyHQ uses default-deny, operation-level policy. The same evaluator governs the UI, Explorer, API, internal calls, hooks, jobs, MCP discovery, MCP execution, source control, and tests.

## Policy evaluation

Every request resolves the authenticated principal and evaluates:

~~~text
identity and authentication strength
intersection company membership and role
intersection service/project and operation
intersection record and field constraints
intersection task/Change/worktree scope when delegated
intersection active approval and step-up state
~~~

The result is deny, allow, or allow with query/field constraints. Constraints apply at the source query and DTO projection boundary. A browser, agent, export, or client never receives a broad record set to filter locally.

## Roles

| Role | Intended authority |
| --- | --- |
| Owner | Controls the assigned company, subject to MFA, step-up, and protected approvals. |
| Administrator | Operates staff, services, content, and projects within owner-only recovery and cross-company limits. |
| Operator | Runs approved operational workflows and permitted service data. |
| Editor | Creates and updates assigned content, forms, tasks, and projects without policy or deployment authority. |
| Analyst | Reads permitted metrics, content, and Pipeline data. |
| Developer | Creates governed source Changes and model/DTO proposals in assigned scopes. |

Agent is not a role. A delegated agent inherits no standing authority.

## Delegated-agent parity

An agent's effective access is the non-additive intersection of the delegating staff member's current access, the enabled provider profile, assigned agent role, approved skills/tools, task, service/project, Change, worktree, and approval. If any input narrows or expires, the capability is reissued more narrowly or revoked and the run stops.

The agent authenticates with its own short-lived capability and is audited with both the technical identity and human delegator. It cannot use a staff browser session or call a private internal path to escape policy.

## Record and field policy

Every model field declares read, create, update, classification, and DTO inclusion rules. Admin and client eligibility is selected per field and rechecked by company, role, operation, record, API version, and delegation scope.

Hidden fields remain absent from list, detail, search, relationship expansion, export, API, MCP, logs, errors, screenshots, notifications, and agent context. Relationship depth and result counts are bounded before serialization.

## Protected actions

Role assignments, policy changes, model-field exposure, secrets, service or agent-provider enablement, source-control connection, production deployment, financial actions, permanent deletion, custom authentication strategies, and account recovery require the relevant policy plus configured MFA, step-up, and approval rules.

An approval binds the exact actor, action, resource, DTO/API version, Change, diff/revision, worktree, and expiry. It cannot authorize a different operation or later revision.

## No bypass paths

Internal APIs, local calls, workers, migrations, hooks, background jobs, tests, admin actions, and MCP handlers all carry an actor and repeat authorization. Tool discovery hides unavailable capabilities; execution checks again. Every security-relevant allow or deny records safe audit context without secrets or hidden field values.
