# Documentation inventory

> **Document status:** Verified inventory as of 2026-07-10.

## Existing and retained

The pre-existing `admin/`, `auth/`, `cms/`, `agents/`, `services/`, and root topical documents contain useful detailed product behavior. They are retained, status-labeled, and linked as supporting material. Focused new documents are authoritative where overlap exists.

## Created to fill missing contracts

This pass added the root README, status vocabulary, glossary, repository assessment/structure, independent-installation/runtime/data/request/deployment diagrams and contracts, CMS compiler/collection/field/database/admin/version/publishing/extension contracts, canonical auth/role documents, provisioning suite, task/BullMQ suite, provider/agent-chat/recovery suite, projects/worktrees suite, 24 MCP documents, security/operations suites, reference studies, ADR directory, and 31-phase roadmap.

## Conflicts corrected

- Central multi-company control plane and owner company switcher → independent one-company installations with no switcher/super-admin.
- Older owner/admin/operator/editor/analyst/developer application roles → owner/admin/developer/client.
- Earlier provisioning profile/flag assumptions → confirmed `{command} -c ... -s ... -u ... -p ...` and `-cf`, with password risk documented.
- Agent merge described as policy-dependent → merge/approval/deployment are never agent capabilities.
- Generic MCP summary → mandatory installation-local suite with per-call authorization.
- Generic relational recommendation → PostgreSQL relational operational state plus validated JSONB content/index projections.

## Missing by design

Verified-implementation documents, generated API references, migration catalogs, runbooks with real commands, performance baselines, SLO values, and release compatibility matrices cannot truthfully exist until code and deployment evidence exist. They must be added in the corresponding roadmap phases.

## Consolidation rule

Do not delete useful older detail merely because a focused contract exists. Update or deprecate it explicitly, link to the authoritative document, and remove only actual duplication or contradiction. Relative links are validated before handoff.
