# Resend email service

## Goal

Provide a toggleable platform email service with encrypted key storage, an admin settings page, and authenticated send API.

## Scope

- Add the Resend platform service, manifest, NuGet dependency, convention tree, and documentation.
- Persist Resend settings and encrypted API keys as parent/child `BaseModel` entities.
- Implement `IEmailService` with idempotency and typed handling for 401, 403, and 429 responses.
- Provide a masked settings page and a ManageUsers-protected, antiforgery-protected send endpoint.

## Task-specific tests

- Verify encrypted key persistence and decrypt round-trip through entity services.
- Fake the HTTP handler and test success, 401, 403, 429 retry, and mandatory idempotency keys.
- Assert endpoint outcomes: anonymous 401, Viewer 403, Admin success, and missing antiforgery rejection.
- bUnit-test that the settings page masks keys and never renders plaintext.
