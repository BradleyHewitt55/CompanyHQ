# Implementation roadmap

> **Document status:** Target V1 recommendation. No phase is implemented.

Each phase ends with passing acceptance/tests and synchronized docs. Security defects block progression. Tasks below are deliberately small enough for coding agents, but agents still require human review and may not merge.

## 1. Monorepo foundations

- **Objective:** Establish boundaries before business code.
- **Deliverables:** pnpm workspace, strict TypeScript, lint/format/typecheck/test/build graph, package export maps, CI.
- **Dependencies:** none.
- **Scope:** root; `apps/web`, `worker`, `scheduler`, `cli`; initial packages from [repository structure](repository-structure.md).
- **Models/APIs:** only health/build metadata contracts.
- **Security:** dependency scanning, lockfile integrity, forbidden-import architecture tests.
- **Acceptance:** clean install and all root checks pass on supported Node.
- **Tests:** package boundary, circular dependency, export-map smoke tests.
- **Docs:** README, contribution/setup and verified-state updates.
- **Coding tasks:** create workspace manifests; add TS configs; scaffold empty apps; add boundary lint rule; add CI.

## 2. Shared configuration and types

- **Objective:** Create canonical Zod envelopes and configuration loading.
- **Deliverables:** `contracts`, `config`, typed IDs, error/result, company/actor/correlation contexts, environment validation.
- **Dependencies:** 1.
- **Scope:** `packages/contracts`, `config`, `company-context`.
- **Models/APIs:** `CompanyContext`, `ActorContext`, command/query/event/job envelopes; config hash.
- **Security:** reject unknown production config; redact secrets; no caller-selected installation.
- **Acceptance:** every app boots from the same validated configuration revision.
- **Tests:** schema round trips, invalid config, redaction, context forgery.
- **Docs:** configuration/status and environment reference.
- **Coding tasks:** define branded IDs; create env loader; create safe error schema; add config hash fixture.

## 3. Development and production Docker setup

- **Objective:** Reproduce application topology locally and securely in production.
- **Deliverables:** Compose templates for web/worker/scheduler/PostgreSQL/Redis/storage; health/readiness; pinned images.
- **Dependencies:** 1–2.
- **Scope:** `infra/docker`, all app composition roots.
- **Models/APIs:** health DTOs.
- **Security:** isolated network/volumes, no Docker socket in app/agent, production config fail-closed.
- **Acceptance:** clean startup, readiness, graceful drain, volume persistence, independent Compose project names.
- **Tests:** two-stack collision test, restart, unhealthy dependency behavior.
- **Docs:** deployment and development/production.
- **Coding tasks:** author dev Compose; add health endpoints; add production override; test unique network/volume names.

## 4. Provisioning CLI

- **Objective:** Idempotently install one company on a remote server.
- **Deliverables:** SSH profiles, host-key verification, preflight/interview, journaled stages, canonical layout, install/inspect commands.
- **Dependencies:** 2–3.
- **Scope:** `apps/cli`, provisioning contracts, `infra/docker`.
- **Models/APIs:** installation journal, server profile, stage result; no public runtime API.
- **Security:** fixed SSH commands, protected secret channel, `-p` scrubbing/warning, no secret persistence.
- **Acceptance:** confirmed CLI formats install and safely resume after interruption.
- **Tests:** hostile names, changed host key, stage replay, pre-existing files, password-log scan.
- **Docs:** all provisioning documents.
- **Coding tasks:** implement parser; SSH verifier; preflight probes; journal store; dry-run and resume.

## 5. PostgreSQL adapter and base models

- **Objective:** Establish context-bound persistence and migrations.
- **Deliverables:** database port/adapter, migration runner/lock, unit of work, outbox base, repository architecture enforcement.
- **Dependencies:** 2–3.
- **Scope:** `packages/database`, `contracts`, `company-context`.
- **Models/APIs:** installations, config revisions, idempotency receipts, outbox, audit stub.
- **Security:** separate credentials; automatic installation constraints; parameterized queries; no public SQL.
- **Acceptance:** transactional commit/rollback and optimistic conflict work; forbidden imports fail CI.
- **Tests:** migration up/down compatibility, concurrent updates, context mismatch, SQL injection.
- **Docs:** database adapter and repository structure.
- **Coding tasks:** choose driver/query builder; add migration 001; implement unit of work; add test container fixture.

## 6. Authentication and roles

- **Objective:** Secure owner/admin/developer/client access.
- **Deliverables:** users, memberships, sessions, password/MFA/recovery, central policy evaluator, step-up and audit events.
- **Dependencies:** 5.
- **Scope:** `auth`, `authorization`, `database`, `apps/web`.
- **Models/APIs:** users, credentials, sessions, roles/grants, policy versions; auth route handlers.
- **Security:** Argon2id, opaque cookies, CSRF/origin, enumeration resistance, revocation, rate limits.
- **Acceptance:** protected admin and policy matrices work; no internal bypass.
- **Tests:** session fixation/replay, CSRF, role/field/query matrices, revoked/stale sessions.
- **Docs:** authentication, roles, authorization.
- **Coding tasks:** implement password service; session repository; login/logout; policy evaluator; owner bootstrap.

## 7. CMS schema metadata

- **Objective:** Compile configuration into one immutable runtime registry.
- **Deliverables:** collection/global/field schemas, ordered extension transforms, validation, admin manifest, config revision/type generation.
- **Dependencies:** 2, 5–6.
- **Scope:** `config`, `contracts`, `cms-core`, `extensions`.
- **Models/APIs:** configuration revision and schema metadata; registry inspection query.
- **Security:** collision/unsafe declaration rejection; server/client manifest split.
- **Acceptance:** deterministic registry and generated types from fixture configs.
- **Tests:** transform order, duplicates, cycles, client-secret/function leakage, snapshot compatibility.
- **Docs:** CMS configuration and extensions.
- **Coding tasks:** implement `defineCompanyConfig`; normalize registry; validate routes; emit serializable manifest.

## 8. Collections and fields

- **Objective:** Implement collection/global CRUD and field traversal.
- **Deliverables:** JSONB document repository, canonical field tree, validation/defaults/access, relationships, arrays/blocks/localization.
- **Dependencies:** 5–7.
- **Scope:** `cms-core`, `database`, `authorization`, `contracts`.
- **Models/APIs:** documents, globals, schema revisions; internal create/read/list/update/delete commands/queries.
- **Security:** authorized query predicates, field policy, bounded relationships/filters.
- **Acceptance:** nested configured content persists and conflicts safely.
- **Tests:** each field type, invalid/unknown input, relationship cycles, field denial, concurrency.
- **Docs:** collections and fields.
- **Coding tasks:** field visitor; JSONB mapper; filter compiler; relationship loader; CRUD services.

## 9. DTO projection

- **Objective:** Prevent raw-model exposure on every surface.
- **Deliverables:** DTO registry/projector, versioning, field/relationship limits, safe error projection.
- **Dependencies:** 6–8.
- **Scope:** `contracts`, `cms-core`, `authorization`.
- **Models/APIs:** admin/public/preview/agent/API/MCP DTO definitions.
- **Security:** allowlists, unknown stripping, recursive reauthorization, response limits.
- **Acceptance:** no route/service returns database rows; hidden fields absent from all projections.
- **Tests:** omission, nested relationships, errors/logs/caches, version compatibility.
- **Docs:** DTO contracts and API versioning.
- **Coding tasks:** build projector; add DTO registration; add static raw-model export check; fixture matrices.

## 10. Public website routing

- **Objective:** Serve published company content at `/`.
- **Deliverables:** Next.js public route group, configured page resolver, locale/SEO/cache/preview contract.
- **Dependencies:** 8–9.
- **Scope:** `apps/web`, `cms-core`, `config`.
- **Models/APIs:** public query DTOs, preview token.
- **Security:** published-only default, preview revision binding, cache installation/config keys.
- **Acceptance:** configured pages render correct published revision and 404 safely.
- **Tests:** draft leakage, preview expiry, locale fallback, cache invalidation.
- **Docs:** routing, SEO, content lifecycle.
- **Coding tasks:** public layout; route resolver; preview verifier; page query cache.

## 11. Admin routing

- **Objective:** Resolve generated authenticated `/admin` routes.
- **Deliverables:** admin manifest resolver, layouts/providers, authorized navigation, list/detail/custom route shells.
- **Dependencies:** 6–9.
- **Scope:** `apps/web`, `admin-ui`, `config`.
- **Models/APIs:** admin manifest/list/detail DTOs and queries.
- **Security:** server-side auth/policy on every route; client manifest contains no server code.
- **Acceptance:** dashboard, collection/global and account routes resolve from registry.
- **Tests:** unauthorized direct URL, nav omission, route collision, manifest mismatch.
- **Docs:** admin routing and admin overview.
- **Coding tasks:** route catch-all; view registry; nav builder; error boundaries.

## 12. Default admin pages

- **Objective:** Deliver usable config-driven CMS screens.
- **Deliverables:** dashboard, list/detail/create, field registry, forms, validation/errors, account/auth, responsive/accessibility baseline.
- **Dependencies:** 11.
- **Scope:** `admin-ui`, `apps/web`, `cms-core`.
- **Models/APIs:** form state and mutation result DTOs.
- **Security:** no trust in hidden/disabled controls; CSRF and reauthorization on submit.
- **Acceptance:** authorized user completes CRUD with optimistic conflict handling.
- **Tests:** component/field forms, accessibility, validation mapping, double-submit.
- **Docs:** admin UI and fields.
- **Coding tasks:** form store; core field components; list table; detail actions; auth pages.

## 13. Admin override resolver

- **Objective:** Support sparse company components/pages with safe fallback.
- **Deliverables:** public slot SDK, generated override registry, contract/version checks, default fallback.
- **Dependencies:** 7, 11–12.
- **Scope:** `admin-ui`, `extensions`, installation `admin/`.
- **Models/APIs:** slot/component manifest.
- **Security:** no private imports or auth/policy replacement; CSP/bundle review.
- **Acceptance:** valid override loads; missing/invalid safe override falls back or blocks deploy as classified.
- **Tests:** version mismatch, route collision, private import, failure boundary.
- **Docs:** admin overrides and explorer overrides.
- **Coding tasks:** define slots; registry generator; fallback resolver; sample installation override test.

## 14. Drafts and versions

- **Objective:** Preserve every change and support restore.
- **Deliverables:** revisions, draft/published pointers, autosave, history/diff, restore-as-new-revision.
- **Dependencies:** 8–9, 12.
- **Scope:** `versions`, `cms-core`, `database`, `admin-ui`.
- **Models/APIs:** revisions, version tokens; autosave/history/restore commands.
- **Security:** historical access/DTO checks; idempotent autosave; encrypted secret handling.
- **Acceptance:** concurrent edits conflict; restore never rewrites history.
- **Tests:** autosave dedupe, version conflict, history projection, restore integrity.
- **Docs:** drafts and versions.
- **Coding tasks:** revision migration/repository; version middleware; history UI; restore command.

## 15. Publishing and approval

- **Objective:** Govern review, approval, scheduling, and publication.
- **Deliverables:** workflow state machine, bound approvals, separation of duty, schedules, publish/unpublish.
- **Dependencies:** 6, 14.
- **Scope:** `workflows`, `versions`, `cms-core`, `admin-ui`, `scheduler`.
- **Models/APIs:** workflow transitions, approvals, schedules; review/publish commands.
- **Security:** exact revision/policy/expiry binding; recheck at scheduled execution.
- **Acceptance:** invalidated approvals cannot publish; authorized workflow completes atomically.
- **Tests:** all transitions, self-approval denial, stale revision, missed schedule recovery.
- **Docs:** publishing and content workflows.
- **Coding tasks:** state machine; approval service/UI; schedule repository; publication pointer command.

## 16. Media and storage

- **Objective:** Secure upload, processing, and delivery.
- **Deliverables:** storage port/local+object adapters, multipart upload, scan/metadata/variants, private/public delivery, usage/retention.
- **Dependencies:** 5–9, 15.
- **Scope:** `storage`, `cms-core`, `worker`, `admin-ui`.
- **Models/APIs:** media assets/variants/usages; upload/finalize/download commands.
- **Security:** type/size/content checks, random keys, no traversal, signed delivery, installation prefix.
- **Acceptance:** upload-to-published delivery and deletion safeguards work independently per installation.
- **Tests:** polyglot/malware fixture handling, path collision, authorization, orphan cleanup/idempotency.
- **Docs:** media, storage ADR, backup coverage.
- **Coding tasks:** storage interface; local adapter; upload session; scan job; media admin field.

## 17. Hooks and extensions

- **Objective:** Add controlled lifecycle and plugin extensibility.
- **Deliverables:** public SDK, hook runner, capability manifest, install/upgrade validation, after-commit integration.
- **Dependencies:** 7–9, 16.
- **Scope:** `extensions`, `cms-core`, `worker`.
- **Models/APIs:** extension install/revision/health; extension registry query.
- **Security:** provenance/license review, no private/database imports, bounded transactional hooks, declared egress/secrets.
- **Acceptance:** sample extension contributes config/hook/admin safely and can be disabled.
- **Tests:** hook order/error/transaction, malicious imports, collision, upgrade/rollback.
- **Docs:** extensions, hooks, custom service guide.
- **Coding tasks:** SDK package; manifest schema; hook executor; sample extension contract fixture.

## 18. Custom endpoints

- **Objective:** Expose versioned extension APIs without bypassing services.
- **Deliverables:** endpoint registry/router, schema/error/rate/idempotency/audit middleware, webhook ingress pattern.
- **Dependencies:** 6, 9, 17.
- **Scope:** `apps/web`, `extensions`, `contracts`.
- **Models/APIs:** endpoint and webhook declarations; `/api/v1/...`.
- **Security:** central auth/policy, strict inputs, signature/replay for webhooks, no raw repositories.
- **Acceptance:** registered endpoint works through query/command service and collisions fail compilation.
- **Tests:** auth matrix, malformed/oversized input, duplicate routes, webhook replay.
- **Docs:** custom endpoints and routing.
- **Coding tasks:** endpoint declaration; route dispatcher; safe error mapper; webhook verifier.

## 19. Redis caching

- **Objective:** Add reconstructible caches and distributed coordination.
- **Deliverables:** namespaced cache adapter, TTL/tag invalidation, rate-limit primitives, safe locks.
- **Dependencies:** 2–3, 9–10.
- **Scope:** `queue`/cache package, web/worker composition.
- **Models/APIs:** cache key/tag contracts; no business models.
- **Security:** installation/environment/config/policy keys, DTO-only values, no secrets, bounded cardinality.
- **Acceptance:** Redis flush causes no data loss; publication invalidates exact public/admin caches.
- **Tests:** cross-install key collision, stale policy/config, stampede, Redis unavailable.
- **Docs:** architecture data flow and operations.
- **Coding tasks:** key builder; cache port; tag index; resilient read-through wrapper.

## 20. BullMQ tasks

- **Objective:** Execute durable background and scheduled work.
- **Deliverables:** queue registry, Zod jobs, outbox publisher, workers, PostgreSQL leases/heartbeats, retries/DLQ/cancel/idempotency.
- **Dependencies:** 5, 19.
- **Scope:** `queue`, `orchestration`, `apps/worker`, `scheduler`.
- **Models/APIs:** tasks, runs, attempts, leases, schedules, idempotency, DLQ incidents; task commands/queries.
- **Security:** signed company/actor context, reauthorization, payload excludes secrets/documents.
- **Acceptance:** crash/restart/retry/cancel cannot duplicate a mutation or lose authoritative state.
- **Tests:** worker kill, lock expiry, duplicate delivery, retry classes, DLQ replay, Redis rebuild.
- **Docs:** tasks, BullMQ, recovery.
- **Coding tasks:** migrations; queue factory; run claim/heartbeat; outbox pump; DLQ admin query.

## 21. Project resources

- **Objective:** Register and govern installation-owned source projects.
- **Deliverables:** project CRUD, canonical paths, scripts, environment/health metadata, access policy.
- **Dependencies:** 6, 20.
- **Scope:** `source-control`, `apps/web`, `database`.
- **Models/APIs:** projects, scripts, environments; project admin queries/commands.
- **Security:** paths below `projects/`, credential references only, script IDs not shell strings.
- **Acceptance:** authorized developer registers/inspects project without filesystem escape.
- **Tests:** duplicate repo, hostile/symlink paths, policy, deleted/moved project.
- **Docs:** projects overview.
- **Coding tasks:** project migration/repository; path resolver; health query; admin screen.

## 22. Source-control integration

- **Objective:** Provide Git and GitHub through separate adapters.
- **Deliverables:** VCS capability interface, safe process runner, Git status/diff/refs/commit/push, hosted provider PR/status adapter.
- **Dependencies:** 20–21.
- **Scope:** `source-control`, `agent-tools`, `admin-ui`.
- **Models/APIs:** repository identity, VCS status, operation receipts, PR references; governed commands.
- **Security:** sanitized env/arguments, output/time limits, protected refs, credential broker, separate action approvals.
- **Acceptance:** status/diff and approved branch actions work; protected operations are impossible through adapter.
- **Tests:** command injection, timeout/output, remote mismatch, force/merge denial, ambiguous push reconciliation.
- **Docs:** source control and source-control boundaries.
- **Coding tasks:** process runner; Git driver; GitHub adapter; status UI; policy gates.

## 23. Git worktrees

- **Objective:** Isolate source-changing tasks.
- **Deliverables:** task branch/worktree create/lease/checkpoint/diff/quarantine/cleanup lifecycle.
- **Dependencies:** 20–22.
- **Scope:** `source-control`, `orchestration`, `worker`.
- **Models/APIs:** worktrees, leases, checkpoints; create/status/revert/cleanup commands.
- **Security:** canonical path/symlink checks, one parent owner, no protected-main write, no automatic uncertain cleanup.
- **Acceptance:** subtasks share exactly one worktree and competing writers serialize.
- **Tests:** concurrent claims, crash/orphan, symlink/submodule escape, cleanup preconditions.
- **Docs:** worktrees and project task workflow.
- **Coding tasks:** naming/path service; Git worktree adapter; lease repository; checkpoint store.

## 24. Agent adapter contract

- **Objective:** Normalize external provider runtimes.
- **Deliverables:** provider catalog/instance/profile, canonical lifecycle/events, streaming, approvals/input, usage, cancel/resume, mock adapter.
- **Dependencies:** 2, 6, 20, 23.
- **Scope:** `agents`, `agent-adapters`, `contracts`.
- **Models/APIs:** provider instances, profiles, sessions, canonical events/cursors; connection admin commands.
- **Security:** disabled default, secret references, CompanyHQ policy outranks provider modes, bounded output.
- **Acceptance:** mock provider completes, cancels, disconnects, and resumes a task deterministically.
- **Tests:** event ordering/dedup, stale session, pending approval abort, token/cost accounting.
- **Docs:** agent connections, roles, reconnection.
- **Coding tasks:** schemas; adapter interface; registry; mock driver; session directory/reaper contract.

## 25. Codex, Claude, Pi, and Hermes connections

- **Objective:** Implement and capability-test external adapters.
- **Deliverables:** provider drivers, local/remote transports where supported, health/config UI, schema/version compatibility.
- **Dependencies:** 24.
- **Scope:** `agent-adapters`, `worker`, `admin-ui`.
- **Models/APIs:** provider-specific opaque state behind canonical session records.
- **Security:** isolated process/container, minimal env, network allowlist, no provider-native authorization bypass.
- **Acceptance:** each enabled adapter passes canonical conformance; unsupported Pi features keep it disabled.
- **Tests:** mock/fixture conformance, reconnect, rate/error, credential redaction, cancellation.
- **Docs:** provider-specific docs and Hermes local/remote ADR.
- **Coding tasks:** implement one adapter at a time; add contract suite; health probe; admin configuration.

## 26. Agent Chat

- **Objective:** Give humans a durable control/review surface for runs.
- **Deliverables:** thread/messages/activity, streaming transport, plan/tool/approval/input cards, costs, changed files, cancel/reconnect.
- **Dependencies:** 20, 24–25.
- **Scope:** `apps/web`, `admin-ui`, `agents`, `orchestration`.
- **Models/APIs:** threads/messages/activities/pending requests; stream/query/response commands.
- **Security:** authorized history/DTOs, untrusted content labels, safe attachments, no raw tool secrets.
- **Acceptance:** chat survives refresh/disconnect and correctly resolves approval/input/cancel.
- **Tests:** stream order/replay, cross-user/task isolation, attachment traversal, stale response.
- **Docs:** agent chat and required diagram.
- **Coding tasks:** chat projections; event stream; composer/timeline; approval/input handlers; cost panel.

## 27. MCP server and tools

- **Objective:** Ship the mandatory governed agent boundary.
- **Deliverables:** local/remote transports, auth/capabilities, registry, CMS/task/project/source/connection tools, resources/prompts, audit/telemetry.
- **Dependencies:** 6, 9, 20–26.
- **Scope:** MCP app/module, `agent-tools`, `contracts`, application services.
- **Models/APIs:** MCP sessions/capabilities/call receipts; all contracts in `docs/mcp`.
- **Security:** per-call auth, no DB/arbitrary shell/fs/network/secrets, strict DTO/limits/revocation.
- **Acceptance:** coding/marketing profiles discover different catalogs and cannot escape task/company scope.
- **Tests:** protocol conformance, token replay/rotation, call policy, injection/path, idempotency, local/remote parity.
- **Docs:** all 24 MCP documents.
- **Coding tasks:** registry; token issuer; stdio transport; remote transport; first read tool; mutation receipt; security suite.

## 28. Recovery and reconnection

- **Objective:** Recover safely from crashes, transport loss, and uncertain effects.
- **Deliverables:** startup reconciler, one retry owner, provider/session/worktree/job reconciliation, checkpoints, stale-attempt fencing.
- **Dependencies:** 20, 23–27.
- **Scope:** `orchestration`, `agents`, `queue`, `source-control`, workers.
- **Models/APIs:** recovery incidents/actions, reconciliation receipts; operator retry/quarantine commands.
- **Security:** reauthorize after resume; never replay approvals/capabilities; quarantine ambiguity.
- **Acceptance:** kill every component mid-flow and reach a correct terminal/review state without duplicate effects.
- **Tests:** fault injection at each boundary, provider mismatch, Redis loss, expired approval, stale completion.
- **Docs:** task recovery, agent reconnection, failure recovery.
- **Coding tasks:** reconciler registry; fencing tokens; recovery dashboard query; chaos test scripts.

## 29. Auditing and observability

- **Objective:** Make every human/agent mutation explainable and operable.
- **Deliverables:** immutable audit service/export, OpenTelemetry traces/metrics, structured redacted logs, dashboards/alerts/run history.
- **Dependencies:** all earlier vertical flows.
- **Scope:** `audit`, `observability`, all apps/adapters.
- **Models/APIs:** audit events/exports, incident references; operator queries.
- **Security:** append-only access, tamper evidence, redaction, least-privilege telemetry, audit fail-closed for mutations.
- **Acceptance:** one correlation traces UI/MCP through command/outbox/job/provider/tool; no seeded secrets appear.
- **Tests:** audit completeness/tamper, redaction corpus, telemetry cardinality, sink outage.
- **Docs:** operations suite.
- **Coding tasks:** audit migration/writer; OTel bootstrap; redaction library; baseline dashboards/alerts.

## 30. Security and negative tests

- **Objective:** Prove boundaries under adversarial behavior.
- **Deliverables:** threat-model test matrix, two-installation harness, prompt/tool/webhook/source/storage attacks, dependency/container scanning, fuzz/property tests.
- **Dependencies:** 3–29.
- **Scope:** `testing`, all apps/packages/infra.
- **Models/APIs:** test fixtures only.
- **Security:** independent review; release-blocking severity policy; no production secrets/data.
- **Acceptance:** all cross-company and privilege-escalation cases deny at the source boundary.
- **Tests:** the phase is the test gate; include race/failure/restore and provider compromise.
- **Docs:** threat model, cross-company tests, security baseline.
- **Coding tasks:** create second-stack fixture; token/job/resource substitution matrix; path/SSRF fuzzers; architecture scan.

## 31. Deployment, backup, upgrade, and restore

- **Objective:** Operate V1 safely through its lifecycle.
- **Deliverables:** pinned releases, migration/drain gates, encrypted backups, restore/upgrade/rollback CLI, runbooks, SLOs.
- **Dependencies:** 3–30.
- **Scope:** `apps/cli`, `infra/docker`, database/storage/queue/operations.
- **Models/APIs:** deployments, backups, restore drills, upgrade journals; operator health endpoints.
- **Security:** signed/pinned artifacts, protected backup keys, human deployment authority, no agent deploy tools.
- **Acceptance:** install two companies, upgrade one independently, restore to quarantine, pass integrity/isolation, and document RPO/RTO.
- **Tests:** failed migration, image rollback compatibility, corrupt/missing backup parts, secret rotation, disaster drill.
- **Docs:** deployment, provisioning upgrades/backup, runbooks and verified implementation state.
- **Coding tasks:** release manifest; drain/migration coordinator; backup adapters; restore verifier; end-to-end drill.

## Recommended first implementation task

Create Phase 1's minimal pnpm monorepo with empty `apps/web`, `apps/worker`, `apps/scheduler`, and `apps/cli`, plus `packages/contracts` and `packages/company-context`. Add strict TypeScript, explicit export maps, root build/typecheck/test commands, and an architecture test that fails if an application imports another package's private `src` path. Do not add CMS features until this boundary scaffold passes CI.
