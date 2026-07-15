# Cloudflare R2 storage service

## Goal

Provide a toggleable platform object-storage service backed by Cloudflare R2 with encrypted credentials and an admin settings page.

## Scope

- Add the CloudflareR2 platform service, manifest, AWS S3 dependency, convention tree, and documentation.
- Persist R2 settings and encrypted secrets as parent/child `BaseModel` entities.
- Implement `IObjectStorageService` for put, get, delete, list, and presign.
- Configure the R2 endpoint, `auto` region, path-style access, required-only checksums, safe key validation, and truncated-list pagination.

## Task-specific tests

- Assert S3 client configuration uses the account endpoint, `auto` region, path style, and required-only checksum settings.
- Verify encrypted secret persistence and ensure the settings page never echoes plaintext.
- Test put/get/delete/list pagination against a fake S3 client.
- Assert traversal and leading-slash object keys are rejected.
- Add an optional env-gated live R2 test marked `External`.
