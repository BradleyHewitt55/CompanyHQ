# Email service

Email owns CompanyHQ's transactional message contract, controlled template catalog, HTML rendering
and validation, packaged defaults, active administrator overrides, and delivery through the official
`Resend` package. It does not own the Resend API key or client registration, which the host supplies,
and it never exposes arbitrary public sending.

## Rules

- Callers choose a controlled template key and provide structured values. They must not build
  transactional HTML bodies or pass arbitrary template paths.
- Ordinary values are HTML-encoded before insertion. Required placeholders, unknown placeholders,
  unresolved output, scripts, event handlers, and javascript URLs fail deterministically.
- **Every email goes through the `queued_email` outbox.** `IEmailService` validates the message and
  stores it; it never calls the provider. A successful result means durably queued, not delivered,
  and `EmailId` is the queue row id. `EmailQueueProcessor`, run by `EmailWorker`, is the only caller
  of `IEmailProvider`.
- One row per idempotency key, enforced by a unique index. A repeat send of the same business event
  returns the existing row, which also absorbs the Notification worker's at-least-once repeats. Keys
  are therefore permanent, not Resend's 24 hours: a caller that genuinely needs a second email for
  the same event needs a new key.
- `IEmailService` writes through its own DI scope. Saving on the caller's context would flush whatever
  else that context is tracking, mid-transaction.
- The worker claims `BatchSize` (10) due rows with `FOR UPDATE SKIP LOCKED`, priority first, under a
  10-minute lease, and saves each row's outcome as soon as its send returns. Retryable failures back
  off (1s doubling to 5 min) and are abandoned after five attempts; a rejection fails at once.
- A send is one Resend call. Network failures, timeouts, 429, quota and 5xx results are retryable and
  go back to the outbox; nothing retries inside the send. Failures log the status code and error type
  only, never the Resend message, recipients, subject or bodies.
- `EmailSendPacer` starts every send at least `1 / Email:MaxSendsPerSecond` after the previous one
  (default 10; a Resend team is limited to 2/s unless raised). It is per process and `Retry-After`
  is ignored.
- **Bodies are wiped when a row leaves `Pending`.** They carry reset links and temporary passwords.
  Recipients, subject, status, and the provider id stay as the send log.
- Persisted HTML overrides are active at send time. Missing overrides use the packaged `.html` default;
  defaults are never copied over an existing administrator edit automatically.
- Urgency is template metadata. `EmailTemplateDefinition.IsPriority` (password reset)
  flows onto the queued row, and the worker claims those rows first. Callers never choose priority,
  so a new caller cannot jump the queue by accident.
- The template management routes require their specific saved role permissions and support list, read, update, preview, and
  explicit reset. Preview never sends a message and rendered bodies are never logged.

## Decisions

The default catalog is compiled into the Email assembly and also copied to output/publish so the files
remain directly inspectable. Runtime persistence stores only controlled-key overrides and metadata,
using the shared provider-neutral Persistence ports; EF declarations remain only as deployment schema mappings. Subjects and plain text stay provider-neutral catalog metadata while
the editable HTML body lives in the packaged or persisted template.

Emails are rendered when queued, not when sent, so an administrator edit does not change an email
already waiting. Sent rows are kept (bodies wiped) as the send log and for idempotency; nothing
purges them yet.

There is no CompanyHQ Resend package: wrapping a client Resend already publishes only repeated it.
Email references the `Resend` package and takes its `IResend` from the host, so the key, base
address and HTTP pipeline stay the host's to configure. `IEmailProvider` remains as the seam between
the queue and Resend so the queue can be tested without sending.

## Traps and wiring

The host calls the package's `AddResend(apiKey)` before `AddEmail(configuration)`; `Email:From` is
the verified sender. No other service references Email: Auth, Contact and Notification each declare their own
mail port, and `Apps/Api/Integrations` fills it with `IEmailTemplateSender`. Their template keys
still live in `EmailTemplateKeys`, because the catalog is Email's. **The processor is scoped and takes the provider in its constructor on purpose:** that is what
lets the host's build-time validation refuse to start when the Resend client is not registered.
Resolving the provider from a scope inside the worker would hide that until the first queued email.
Generate a migration whenever the template or queue model changes; the model itself is discovered.
Test hosts can replace only `IEmailService` and still exercise real template rendering; the worker
then finds an empty queue. In-memory test contexts must discover models from the API assembly
(`PostgresModelRegistry.Discover`): EF caches one model per context type, so a test that discovers
fewer assemblies breaks whichever test runs after it.
