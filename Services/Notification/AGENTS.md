# Notification service

`Services/Notification` owns CompanyHQ's durable notification queue, registered devices, two
boolean opt-out preferences, and push delivery through the official `FirebaseAdmin` package,
including the optional service-account credential and the named `FirebaseApp`. It accepts opaque
`NotificationRequest` values through `INotificationService`; it references only foundation and no
feature service.

## Rules

- Enqueue only. There is no public send endpoint and no notification inbox/history.
- Persist the caller's `Channels` on the queue row. The effective set is
  `request.Channels & recipientPreferences`; a caller can narrow delivery but never widen it.
- `NotificationChannels.All` intentionally differs from `ResponseAudience.Client = 0`: a forgotten
  audience argument under-shares data, while a forgotten channel argument must not silently drop a
  notification.
- A missing preference row means both channels are enabled. Write a row only when a user opts out.
- The own-preferences HTTP action is marked for MCP; both paths call `GetPreferencesAsync` with the
  authenticated user's id. Over HTTP any signed-in account reads and writes its own preferences; only
  the MCP tool additionally needs `get_my_notification_preferences` on the role.
- The worker claims small Postgres batches with `FOR UPDATE SKIP LOCKED`, retries transient provider
  failures only, and abandons a row after five attempts. It commits a one-hour `NextAttemptAt`
  claim lease before any provider call; delivery and the result save happen after that transaction,
  so a slow provider never holds row locks or an idle database transaction. An expired lease makes
  rows from a crashed worker eligible again. A missing enabled device/email resolves to `Sent` with
  nothing delivered.
- **Delivery is at-least-once, not exactly-once, and that is inherent to the design above.** A crash
  between a provider call succeeding and the batch's result save leaves the row `Pending` with its
  attempt uncounted, so once the lease expires another worker sends it again. Nothing is lost
  silently; a duplicate is possible. Do not "fix" this by moving the provider call back inside the
  transaction — that reintroduces idle-in-transaction and pool exhaustion exactly when a provider is
  slow. Closing it properly needs a provider-side idempotency key, which no plan has asked for yet.
- The claim orders due rows by `next_attempt_at, id`; no kind jumps the queue.
- **The one-hour lease is also the worst-case recovery time for a crashed worker's in-flight batch.**
  Revisit the lease length before any latency-sensitive kind is added.
- Firebase `TokenDead` disables that device immediately and never retries it. Unconfigured Firebase
  skips push rather than creating a retry storm.
- Missing or invalid Firebase credentials log one warning and disable sending; they never prevent
  the host from starting. An unconfigured send returns `PermanentFailure` for every token:
  retrying an unconfigured provider is a retry storm, not resilience.
- Push payloads are data-only. Title and body are fields in the data map, never an FCM notification
  payload, so Android foreground and background delivery use the same deep-link path.
- Push requests are chunked at the 500-token multicast limit and return one outcome per token.
  `UNREGISTERED` or `NotFound` becomes `TokenDead`, network or timeout or provider 5xx becomes
  `TransientFailure`, other provider failures become `PermanentFailure`. Malformed caller input
  throws `ArgumentException`.
- Never log tokens, credential JSON, message bodies, or file contents.
- Every kind goes to push and email, filtered only by the recipient's preferences. No notification
  route accepts a location, radius, or distance.
- Email render is per kind with a generic fallback (the kind-to-template choice is in the host's
  `NotificationEmailSender` bridge), sends carry a deterministic queue derived idempotency key, and provider timeouts map to transient so one stalled call cannot stop the worker.
- The email channel is Notification's own optional `INotificationEmailSender` port; with no bridge it
  is skipped, not failed. `Apps/Api/Integrations` bridges it to Email's `queued_email` outbox, and
  Email's worker sends it. "Sent" here means queued there. Because the outbox keeps one row per idempotency key, a
  duplicate delivery of the same notification row never becomes a second email, though a duplicate
  push still can.
- Notification bodies must be safe for a lock screen. Message callers send a sender summary, never
  personal message content.
- Device summaries never include the FCM token. Tokens are accepted only by the authenticated
  register/delete routes and are never rendered in admin UI or written to logs.
- Keep the authenticated controller inside this project. `AddNotifications(configuration)` mounts this assembly
  as an application part; putting the controller in a host or repository-level folder silently
  leaves the four documented routes out of the API.
- `Apps/Api` is the only host and calls `AddNotifications(configuration)` once; it owns the live
  queue worker and notification routes. The named `FirebaseApp` is created there, and a second
  registration under the same app name throws, so never call it from another service's registration. There is no read-only registration for a second host — the admin console is
  the Next.js `Apps/Web`, which reads through the API like every other client.
- `EnqueueAsync` and `EnqueueManyAsync` add queue rows to the caller's scoped
  `IPersistence` unit of work; they deliberately do not call `SaveChangesAsync`. A domain operation that
  enqueues notifications persists them with its own unit of work, so enqueueing cannot flush
  unrelated tracked changes at an unexpected point.

The service owns models mapped by the Postgres adapter; they are discovered because the API references this project.
Notification does not reference Cache — it has nothing to cache.

There is no CompanyHQ Firebase package: it only wrapped the SDK Google already publishes.
Notification references `FirebaseAdmin` and builds the app itself rather than taking one from the
host, because push is optional and a required host-registered client would make it mandatory.
`IPushSender` remains as the seam between the queue and FCM so delivery can be tested without
sending. `Firebase:ServiceAccountJson` (`Firebase__ServiceAccountJson`) accepts raw JSON or a path;
relative paths resolve from the repository root beside `.env`, not the process working directory.
The service-account JSON belongs at a gitignored `.key/` or `.keys/` path; `google-services.json`
is client configuration and stays with the mobile app.

## Deliberately not built

No admin send/broadcast action, inbox/history endpoint, per-kind preference, digest, quiet hours,
SMS, APNs, web push, native Windows/macOS toast, FCM topic, or delivery report is part of this
service.
