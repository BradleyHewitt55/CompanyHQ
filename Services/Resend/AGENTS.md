# Resend service

Resend is CompanyHQ's delivery adapter. It owns the Resend API key, sender identity, HTTP transport,
provider response mapping, and bounded retry policy; it does not own templates, persistence, or
domain recipient lookup.

## Rules

- `IEmailProvider` is the only Email dependency crossing into this project.
- Provider failures return provider-neutral `EmailSendResult` values and never expose response bodies,
  recipients, subjects, HTML, or API keys in logs.
- Retry only network failures, 429 responses, and 5xx responses. Caller cancellation is propagated.
- Resend configuration is validated when the host registers this adapter, not on the first send.

## Configuration

Section `Resend` (renamed from `Email` when delivery moved here); from the environment,
`Resend__ApiKey` and so on. The key is baked into the typed `HttpClient` at registration, so rotating
it needs a restart.

| Key | Notes |
| --- | --- |
| `Resend:ApiKey` | Must start with `re_`. Startup throws otherwise. |
| `Resend:From` | Must contain `@` and be a Resend-verified domain. `onboarding@resend.dev` is sandbox only. |
| `Resend:MaxRequestsPerSecond` | Default `10`. Must not exceed the team's Resend limit, which is 2/s unless raised. |

## Rate limit

`ResendRateLimiter` is a singleton pacer: every HTTP attempt, retries included, starts at least
`1 / MaxRequestsPerSecond` seconds after the previous one. It does no ordering and holds no queue.
The queue, priority, and retry-with-backoff all live in Email's `queued_email` outbox, and Email's
worker is the only caller of this provider. The limit is per process. `Retry-After` on a 429 is still
ignored; the pacing is what keeps a burst below the limit.

## Decisions and wiring

The raw `HttpClient` remains intentional because this adapter uses one endpoint and avoids an SDK
dependency. `Services/Email` registers the template and message pipeline first, then the API host
registers `AddResend` to supply the provider implementation. No controller or database model belongs
here.
