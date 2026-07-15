# Plans roadmap

Execution order for the remaining plans. Waves run **sequentially**; plans inside a wave touch disjoint files and may run as **parallel subagents**.

Already completed (contracts are binding, do not undo):
- **company-registry-boundary** — the SQLite registry holds only routing/deployment metadata (company ID, display name, hostname, database name, deployment status); all tenant data lives in the company Postgres database.
- **company-directory-layout** — companies live at `CompanyHQ/Companies/{company-slug}/` with a normalized, immutable slug.

## Orchestrator rules

1. Work one wave at a time, top to bottom. Spin up one subagent per unchecked plan in the current wave, all at once — they are file-disjoint by design.
2. Do **not** start the next wave until every plan in the current wave is checked `[x]` here, meaning: all of that plan's own checkboxes are ticked, its task-specific tests pass, and the full solution builds and its test suite is green.
3. Each subagent gets the prompt template below with its `{PLAN}` substituted. The wave-2 database agent runs its two plans sequentially in one lane.
4. If a subagent dies or stalls, start a replacement with the same prompt — the plan file's checkboxes are the resume point. The replacement must verify the last ticked step actually works before continuing.
5. Never run two agents whose plans are in different waves, and never edit a plan from a later wave "while you're in there".
6. Mark the plan `[x]` in this file only after its verification passes; note deviations under **Log** at the bottom.

## Waves

### Wave 1 — independent refactors (5 parallel lanes)

- [ ] `auth-service-folder-convention.md` — Auth service classes only.
- [ ] `postgres-service-folder-convention.md` — Postgres service classes only. Must land before wave 2's database work (same files).
- [ ] `service-scaffold-folder-guarantee.md` — ServiceScaffolder + blueprint trees.
- [ ] `service-settings-pages.md` — ComponentRegistry / admin UI settings routing.
- [ ] `shared-proxy-topology.md` — Compose generation: one shared proxy, no host ports on apps. Must land before wave 2's database work (both edit Compose generation).

### Wave 2 — core platform (2 parallel lanes)

- [ ] `postgres-root-database.md` **then** `shared-postgres-company-isolation.md` — one lane, one agent, in that order. Root Postgres instance, provider-neutral `Database` service (`IDatabaseService<T>`, `BaseModel` ownership), per-company DB/role provisioning, then the isolation hardening/acceptance pass on the same pipeline. Largest plan in the roadmap.
- [ ] `platform-service-tagging.md` — root-service whitelist (Mcp, CloudflareR2, Resend), `platform` tag, scaffold/catalog exclusions. Gates every wave-3 service.

### Wave 3 — routing + platform services (4 parallel lanes)

- [ ] `company-hostname-routing.md` — proxy hostname routes on provision/delete. Needs wave 1 proxy and wave 2 provisioning changes settled (both edit the company-provisioning flow — that is why it is not in wave 2).
- [ ] `resend-email-service.md` — needs platform tagging, settings pages, and `BaseModel` entities from wave 2.
- [ ] `cloudflare-r2-storage-service.md` — same prerequisites; disjoint service directory.
- [ ] `mcp-api-proxy-service.md` — needs platform tagging; hidden service, no settings page.

### Wave 4 — company flows (2 parallel lanes)

- [ ] `company-hostname-provisioning-ui.md` — CLI/admin add flows collect the hostname; needs wave 3 routing to exist.
- [ ] `company-developer-workspace-isolation.md` — needs per-company credentials from wave 2.

### Wave 5 — hosting + migration (2 parallel lanes)

- [ ] `company-website-container-hosting.md` — website container per company, explorer integration, MockServer; needs proxy, routing, root DB, and workspaces.
- [ ] `migrate-legacy-company-scaffolds.md` — migrates legacy stacks into the final shared topology; legacy scaffolds predate website containers, so it does not depend on the other wave-5 lane.

## Subagent prompt template

```
Read C:\Users\hewit\OneDrive\Desktop\Projects\CompanyHQ\CLAUDE.md and AGENTS.md and follow their rules exactly.

Implement the plan at Plans\{PLAN}.md in full. Rules:

1. Read Plans\ROADMAP.md first for binding contracts from completed plans, then read your plan. Its Goal, Scope, and Task-specific tests define your entire scope — touch nothing outside it, no other Plans\*.md files, and never the .repos directory.
2. Before writing any code: if your plan's Scope and Task-specific tests are plain bullets, convert them in place to `- [ ]` checkboxes. This checklist is your progress ledger.
3. Tick each checkbox in the plan file the moment that individual step is implemented and its test passes — not at the end. Another agent must be able to resume from your checkboxes if you die mid-task.
4. If checkboxes are already ticked when you start, you are a resume agent: verify the last ticked step actually builds and passes before continuing from the first unticked one.
5. Write the plan's Task-specific tests as real automated tests and make them pass.
6. After all boxes are ticked, build the full solution and run the entire test suite; fix anything your change broke.
7. Binding contracts you must not violate: the SQLite registry stores only routing/deployment metadata (no tenant data), and companies live at CompanyHQ/Companies/{company-slug}/ with an immutable slug.
8. If the plan conflicts with what you find in the code, stop and report the conflict instead of guessing.
9. When done, report: files changed, tests added, test results, and any deviations.
```

## Log

_Record completed waves, deviations, and conflicts here._
