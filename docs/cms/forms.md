# Forms

Forms are a core CMS capability for defining project forms and collecting company-isolated submissions. A form definition, its rendered client DTO, and its submission DTO are separate contracts.

## Form definition

A form declares a stable key, owner project, enabled locales, fields, labels/help text, validation, conditional visibility, consent text/version, confirmation behavior, submission policy, retention, notification workflow, and allowed integrations.

Supported field behavior includes text, email, number, choice, boolean, date/time, file reference, and structured groups where explicitly enabled. Fields declare required state, length/range, normalization, classification, admin/client visibility, and export policy. Arbitrary executable validation is not accepted from browser input.

## Submission flow

~~~text
load published form DTO -> render project form -> validate client hints
-> submit with form revision and abuse proof -> validate on server
-> enforce origin, rate, size, consent, and field rules
-> store submission and audit event atomically
-> enqueue notifications/integrations -> return generic confirmation DTO
~~~

Server validation is authoritative. The form revision submitted by the client must still be accepted or the server returns a safe refresh-required result. Hidden conditional fields are rejected when the active rules do not allow them.

## Abuse and privacy

Public forms use exact project origins, rate limits, bot controls appropriate to risk, body and file limits, duplicate/idempotency protection, and safe generic errors. Sensitive values never appear in URLs, analytics, notification previews, or logs.

Consent records include the exact consent version, time, project, form revision, and permitted purpose. Retention and deletion follow field classification and company policy. A marketing opt-in cannot be inferred from submitting an unrelated form.

## Administration

Staff list and detail views use separate submission DTOs. Search, export, assignment, status changes, deletion, and integration replay each require explicit policy. Exports are generated asynchronously, encrypted, time-limited, audited, and contain only selected authorized fields.

Agents may draft form copy or propose schema changes within their delegated scope. They cannot view unrestricted submissions, change consent meaning, export contacts, or trigger external delivery without the exact role, task, service, and approval.

## Integrations and tests

After-commit workflows may send notifications or call enabled services. Each integration verifies current configuration, uses idempotency, exposes retry state, and never blocks the submission transaction on a slow provider.

Tests cover schema and conditional validation, origin and abuse controls, tenant/record/field policy, consent versioning, file rules, idempotency, notification redaction, export scope, retention, deletion, provider failure, and disabled integrations.
