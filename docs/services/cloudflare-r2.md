# Cloudflare R2 media service

Cloudflare R2 is an optional object-storage service for company media, generated assets, exports, and approved build artifacts. It complements the database; it is not the database and it is not a raw file share for agents.

## Storage boundary

The recommended initial boundary is a dedicated R2 bucket for each company. If an implementation later uses shared storage, it must provide an equally strong company prefix, identity, policy, backup, lifecycle, and audit boundary. The service must never rely on UI filtering to separate tenants.

The database stores asset metadata and references. The object store holds bytes. Git stores source assets only when they belong in source control, not user uploads or environment data.

## Service responsibilities

- create controlled upload, download, preview, transformation, and deletion flows;
- record owner, company, service/project usage, content type, size, checksum where applicable, and retention state;
- issue time-bound or policy-checked access rather than exposing bucket credentials;
- support public delivery only for assets explicitly marked public by policy;
- record media lifecycle events and related content/project references;
- define backup, retention, restore, and deletion behavior before enablement.

## Agent and Explorer rules

An agent receives only asset-specific, time-limited MCP access for its task. It cannot list another company's objects, mint unrestricted storage credentials, or use storage as an untracked source-code channel.

The Gallery and Explorer expose authorized metadata, usage, and preview. Editing a source-backed asset creates a Change; managing a user upload creates a content/audit operation. The distinction is visible in the UI.

## MCP and API contract

The service exposes policy-aware asset discovery, upload preparation, approved attachment, use/reference inspection, and retention-safe removal operations. All API operations declare corresponding MCP mappings. Responses are bounded and contain references or signed access only when policy permits.

## Failure posture

If R2 becomes unavailable, the service reports degraded media state, queues retryable work, and prevents destructive cleanup. It does not silently substitute a shared local disk or expose bucket credentials to restore access.

## Test requirements

Every R2 Change runs storage-boundary, signed-access, tenant/DTO, upload validation, retention/recovery, and API/MCP policy tests. Run Gallery/admin UI tests when the task changes media browsing, upload, preview, or deletion behavior.
