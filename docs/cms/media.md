# Media

> **Document status:** Target V1 product contract.

Media is a core CMS contract backed by an enabled storage provider. The CMS owns asset identity, policy, metadata, usage, and lifecycle; a storage adapter owns byte storage and delivery primitives.

## Asset lifecycle

~~~text
upload intent -> bounded upload -> quarantine -> verify and scan
-> metadata extraction -> approved variants -> available
-> replace or archive -> retention-aware deletion
~~~

An upload intent binds company, project/service, user, expected type, maximum size, expiry, and destination class. Upload completion verifies actual type, signature, size, checksum, and object ownership before the asset becomes usable. Suspicious files remain quarantined and cannot be previewed publicly.

## Asset record

The internal asset model records stable ID, company, storage reference, checksum, verified media type, size, dimensions/duration where relevant, original filename as untrusted display text, accessibility text, focal/crop metadata, variants, owner, classification, lifecycle state, usage references, retention, and audit data.

DTOs expose only authorized metadata and delivery references. Storage credentials, internal object keys where sensitive, filesystem paths, scan internals, and another company's usage never leave the service boundary.

## Variants and delivery

Variant rules are declared per project or content field: format, dimensions, crop behavior, quality bounds, and animation handling. Generation is idempotent and resource-bounded. A failed variant does not overwrite the original or mark the whole asset healthy.

Public assets use approved public delivery or time-bounded signed access according to classification. Private assets always reauthorize before a delivery reference is issued. Cache keys include the immutable asset revision and variant.

## Replacement and deletion

Replacing media creates a new immutable asset revision and preserves references until the Change is approved. The usage index identifies content, forms, SEO, services, and projects that reference an asset.

Deletion is blocked or converted to archive while required references or retention rules exist. Permanent deletion removes approved variants and source bytes through an auditable job, verifies provider outcome, and preserves the minimum tombstone required for integrity and audit.

## Tests

Tests cover forged types, oversized and malformed files, decompression/resource abuse, malware quarantine, cross-company access, private delivery expiry, metadata escaping, variant limits, replacement rollback, reference-aware deletion, provider retry, backup/restore, DTO omission, and agent restrictions.
