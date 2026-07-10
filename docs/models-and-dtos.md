# Models and DTOs

> **Document status:** Target V1 architecture contract; see `cms/dto-contracts.md` for the authoritative boundary.

CompanyHQ keeps internal models separate from the data returned to a client, admin page, agent, API, MCP tool, export, log, or error. A model stores company data. A Data Transfer Object, or DTO, is the policy-projected view that a specific actor may receive.

No UI or agent reconstructs a model from scratch. It renders the DTO supplied by the registered page or operation.

## Model and DTO boundary

| Item | Purpose | May contain |
| --- | --- | --- |
| Model | Internal service-owned record and storage contract. | All persisted fields, internal references, secrets references, audit metadata, and server-only values. |
| Admin DTO | Data returned to an authorized staff/admin page or its delegated agent. | Only fields the active role and operation may read. |
| Client DTO | Data returned to a customer-facing project. | Only public or customer-authorized fields. |
| Agent DTO | The same DTO the delegating staff member may receive for the assigned operation, narrowed by task scope. | No additive fields. |

The DTO is generated or declared from the model and field policy. It is never a browser-side filter over a raw model.

## Settings workflow

Users with the explicit model-attribute permission use:

~~~text
Settings -> Models & DTOs -> select model -> Add attribute
~~~

The Settings UI creates a governed Change; it does not silently mutate production schema. The Change records:

- model owner and stable field key;
- data type, validation, default, relation, index/search behavior, and migration impact;
- data classification and audit/retention rules;
- whether the field is available to admin, client, both, or neither DTO surface;
- read, create, and update policy by role, operation, record, and field;
- affected list/detail pages, APIs, MCP resources/tools, exports, and integrations;
- DTO version change, test plan, rollback, and documentation impact.

Only a role granted the relevant model-attribute capability may propose, approve, or apply that Change. A service may narrow this further. A developer does not automatically gain access merely because they can edit source.

## Field exposure

For every attribute, the owner chooses one of these presentation scopes:

| Scope | Result |
| --- | --- |
| Admin only | Eligible for authorized admin DTOs; unavailable to client DTOs. |
| Client only | Eligible for authorized client DTOs; unavailable to admin DTOs unless separately granted. |
| Both | Eligible for both surfaces, each still filtered by field policy. |
| Internal | Stored or calculated only; never returned by a DTO. |

Eligibility is not permission. A field marked Both still requires the active role, record, tenant, and operation policy to allow it.

## Page contracts

A list page and its detail page bind separate DTOs:

~~~text
admin.users             -> AdminUserListDto
admin.users.(slug)      -> AdminUserDetailDto
~~~

An override for the users detail page receives only the current AdminUserDetailDto. If that DTO includes name and email, the override and its delegated agent may use user.name and user.email. If a field is absent, it is unavailable; no fallback model query is allowed.

When a permitted user adds an attribute and includes it in an allowed DTO, the field becomes available in the corresponding admin/client page contract, API response, and agent context after the Change is approved and deployed.

## API, MCP, and agent rules

Every API version declares request and response DTOs. Every MCP resource and tool response uses the same or a narrower DTO. Agent prompts receive schema, bindings, and only safe task-relevant data; never raw records, hidden fields, credentials, or broadly paginated datasets.

DTO projection is re-evaluated for each request and tool call. Cached results, logs, errors, exports, search indexes, and notifications must use the same projection rules.

## Required tests

An attribute or DTO Change must prove:

- allowed roles receive the intended field;
- denied roles, users, agents, tenants, and operations do not;
- list, detail, search, export, API, MCP, UI, logs, and errors do not leak a hidden field;
- migration, rollback, API version, and override-page bindings remain valid.

See [testing](testing.md) and [access control](auth/access-control.md).
