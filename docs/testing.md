# Testing and completion evidence

> **Document status:** Target V1 release-gate contract.

Tests are selected by the task's impact, not by a blanket ritual. A Change is complete only when it has the required API, policy, DTO, MCP, UI, security, and documentation evidence.

## Task matrix

| Change type | Minimum evidence |
| --- | --- |
| Documentation only | Link validation, forbidden-term scan, contract consistency, and confirmation that reference repositories were unchanged. |
| UI-only override or component change | UI interaction, visual/selection, DTO-binding, and fallback tests. |
| API, model, DTO, or access change | API contract, policy, field/DTO, versioning, and negative authorization tests. |
| Combined UI and API change | Both UI and API matrices. |
| Service enablement or new service | Manifest/dependency, API/MCP, policy/DTO, job/hook, migration/rollback, health, and required UI tests. |
| Agent/provider/MCP change | Default-disabled state, explicit enable/disable/remove, role/profile/skill resolution, delegation, capability, transport, tool discovery/execution, Pipeline projection, worktree, isolation, and revocation tests. |
| Auth, secrets, deployment, financial, or integration change | Strategy-specific security tests and approval evidence in addition to functional tests. |

## Service test contract

Every service declares its own test plan. It identifies which API, UI, integration, security, migration, performance, and recovery tests apply. A service without a UI does not need a UI test merely to satisfy a checklist; a service that changes UI must run UI tests. A service with both API and UI impact runs both.

The task acceptance criteria decide the exact assertions. Test evidence is attached to the Change with the revision, worktree, environment, actor, and result.

## Required security negatives

At a minimum, affected changes test:

- cross-company, cross-user, cross-role, cross-project, and cross-service denial;
- staff versus delegated-agent parity for permitted and denied record/field/DTO access;
- direct API, internal API, job, hook, MCP, and UI policy parity;
- capability expiry, revocation, task cancellation, service disablement, and approval expiry while an agent run is active;
- protected-main denial, worktree isolation, filesystem/network/secret isolation, and pull-request-only agent delivery;
- hidden DTO field absence in list, detail, search, export, MCP, logs, errors, notifications, and screenshots;
- CSRF, CORS, cookie/session, login/reset/verification abuse, API-key, MFA, and recovery protections when auth changes;
- prompt injection, hostile comments/resources, SSRF, path traversal, command injection, malformed/oversized inputs, and unsafe tool output when agents or MCP change;
- published major-version compatibility and API-to-MCP contract mapping when an API changes.

## Authentication security matrix

Authentication changes also test:

- password hashing parameters, compromised-password rejection, long-password handling, identifier normalization, account/source throttling, lockout, and enumeration-safe responses;
- email verification and recovery purpose separation, randomness, one-way storage, expiry, atomic single use, duplicate requests, approved origins, session revocation, and notification redaction;
- session fixation prevention, opaque cookie values, host-only/Secure/HttpOnly/SameSite attributes, idle/absolute expiry, refresh rotation, concurrent refresh behavior, reuse detection, single-session and revoke-all behavior;
- exact trusted origins, CSRF value binding, unsafe methods/content types, credentialed CORS without wildcard origins, and separate-site negative cases;
- MFA enrolment/removal, challenge transaction binding, replay, throttling, step-up freshness, recovery-code one-time use, and required-method preservation;
- JWT algorithm allowlist, signature, key ID/rotation, issuer, audience, subject, company/project binding, not-before/expiry, unique ID, session/token version, revocation, and forbidden-claim absence;
- API-key one-time display, one-way verifier, constant-time-compatible verification, company/service/API-major/scope/expiry checks, rotation overlap, revocation, browser rejection, and safe administration DTOs;
- custom-strategy deterministic order, credential size/location, signature or proof validation, issuer/audience/expiry/replay, exact redirect/state/nonce behavior, trusted-proxy header stripping, unique subject linking, collision/relink, outage, and local authorization;
- absence of passwords, cookies, bearer values, key secrets, MFA/recovery values, hidden identity fields, and raw models from URLs, browser storage, responses, logs, traces, errors, notifications, exports, and agent context.

## Agent provider matrix

Agent-provider changes test that a clean company has no installed/enabled provider, discovery does not enable a command-line tool, adding a provider creates a disabled configuration Change, and enablement requires a healthy scoped profile. Tests cover profile isolation, role/skill/tool intersection, skill disablement during a run, CLI environment and working-directory isolation, self-hosted instance network/storage separation, task linking and cancellation, three-state Pipeline projection, provider-detail authorization, offline reconciliation, and disable/remove revocation.

## UI and visual handoff tests

For editable UI, test stable component reference generation, hover outline, click-to-pin selection, comment anchoring, minimized/restored composer state, preview message origin/nonce validation, DTO binding, sparse override creation, and deletion fallback to the supplied UI.

Do not treat a CSS class as the test authority. It is diagnostic only; the stable component reference scopes the change.

## Acceptance and review

An agent may run the tests allowed by its delegated capability. It cannot self-approve a protected result. The reviewer sees the task acceptance criteria, test matrix, raw safe evidence, diff, DTO/policy impact, and unresolved failures before approving a pull request or deployment.
