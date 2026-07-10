# Token data

> **Document status:** Target V1 security contract.

Authentication credentials carry the minimum stable identifiers needed to validate a principal. Authorization data is loaded from current CompanyHQ policy instead of being trusted from stale client-held claims.

## Allowed claims

| Claim | Purpose |
| --- | --- |
| `iss` | Exact CompanyHQ issuer. |
| `aud` | Intended service or client audience. |
| `sub` | Stable principal identifier. |
| `sid` | Revocable session identifier. |
| `company_id` or `project_id` | Bound tenant context when the credential is single-context. |
| `jti` | Unique token identifier for replay and audit correlation. |
| `iat`, `nbf`, `exp` | Issue, activation, and expiry times. |
| `token_version` | Server-checked revocation generation. |
| `amr` | Authentication methods completed, such as password and MFA. |

Names, email addresses, roles, permissions, record identifiers, field values, secrets, provider credentials, agent instructions, and full user objects are excluded. If a client needs current user information, it calls `me` and receives a policy-projected DTO.

## Request principal

After credential validation, middleware constructs an internal request principal containing the identity and verified authentication context. The policy service then loads current membership, role, record/field constraints, service/project state, delegation, and approval. Handlers consume that principal; they do not decode credentials independently or trust client-supplied authorization fields.

## Change and revocation behavior

Role changes, company removal, suspension, password recovery, MFA reset, revoke-all, agent-task cancellation, provider disablement, and other security events update revocation state. Because current authorization is not embedded in the credential, permissions narrow immediately even before expiry.

## Data minimization tests

Tests decode representative tokens and assert that only allowed claims exist. They also verify that logs, traces, errors, notifications, URLs, browser storage, and agent context do not contain credential values or hidden identity fields.
