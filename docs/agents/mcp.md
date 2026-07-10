# CompanyHQ MCP gateway

> **Document status:** Target V1 summary; the authoritative detailed suite is `docs/mcp/`.

Each CompanyHQ installation exposes one logical, policy-aware MCP gateway. Provider adapters translate supported runtimes to this stable installation-local contract.

## API parity without unsafe exposure

Every published API operation declares an MCP mapping, DTO version, policy, approval class, audit event, input/output schema, and response limits. The mapping may be a tool, safe resource, or controlled initiation flow. Every published API major and its mapping stay supported.

Parity does not mean every route becomes an executable agent tool. Login, recovery, OAuth redirects, health probes, and webhooks retain safe protocol-specific behavior and cannot be transformed into unauthenticated agent access.

## Authorization

MCP access is default-deny. Every discovery and execution request binds:

- delegated staff principal or approved service identity;
- company, service/project, task, Change, worktree, action, and API/DTO version;
- agent profile/service allowlist;
- capability issuer, audience, expiry, nonce, revocation state, and approval state.

Discovery hides unavailable tools/resources/prompts. Execution rechecks all policy. There is no production access override equivalent.

## Tool families

| Family | Scope |
| --- | --- |
| discovery | Authorized service, DTO, Page Catalog, task, source, and health metadata. |
| changes | Scoped Change plan, review, approval request, and safe revert preparation. |
| workspace | Allowed worktree files, patch, diff, and test evidence; no raw host filesystem. |
| projects/source control | Scoped status, branch, commit, pull request, and preview actions. |
| services | Service draft/validation/configuration/enablement under policy. |
| deployments | Preview, health, promotion, and rollback only with required approval. |
| content/models | Schema-aware DTO operations with record/field policy. |
| pipeline | Permitted personal, staff, agent, and marketing task operations. |

Tool names are stable and versioned:

~~~text
companyhq.<domain-or-service>.<operation>
~~~

## Input, output, and untrusted context

Validate schemas, path/file scope, URL/resource URI, pagination, size, query depth, and output DTO before every handler. Enforce explicit deny lists for arbitrary filesystem, shell, Docker, database, URL, and credential access.

Treat user comments, DOM text/classes, screenshots, email, webhooks, repository content, external pages, MCP resources, and tool output as untrusted data. They can inform a task but cannot become instructions that expand capability. Defend against prompt injection, path traversal, command injection, SSRF, hostile resource URIs, oversized input, and response exfiltration.

All responses use DTO allowlists, safe errors, and bounded result sizes. Never return raw models, connection strings, secrets, full contact lists, hidden fields, or another user's task/source context.

## Transport and audit

Remote transport uses authenticated TLS. Local worker transport is still authenticated and company/task scoped. Capabilities are replay-resistant, short-lived, rotating, revocable, and correlated with an audit event.

Every mutation is tied to an exact Change/worktree where source is touched. Approval tickets bind exact action/diff/revision and expire. Every result records agent, delegator, company, tool/resource, input classification, policy decision, DTO version, output reference, and safe outcome.

## Service contribution requirement

A service may not enable until it declares human roles, delegated-agent access, DTOs, API versions, MCP mappings, input/output rules, approval class, audit events, security constraints, and required tests. See [custom service](../services/custom-service.md) and [security](../security.md).
