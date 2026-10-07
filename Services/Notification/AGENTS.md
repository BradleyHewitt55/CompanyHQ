# Notification service

`Services/Notification` owns CompanyHQ's durable notification queue, registered devices, and two
boolean opt-out preferences. It accepts opaque `NotificationRequest` values through
`INotificationService`; it references only foundation and no feature service.

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
- Keep the authenticated controller inside this project. `AddNotifications()` mounts this assembly
  as an application part; putting the controller in a host or repository-level folder silently
  leaves the four documented routes out of the API.
- `Apps/Api` is the only host and calls `AddNotifications()` once; it owns the live queue worker and
  notification routes. There is no read-only registration for a second host — the admin console is
  the Next.js `Apps/Web`, which reads through the API like every other client.
- `EnqueueAsync` and `EnqueueManyAsync` add queue rows to the caller's scoped
  `PostgresDbContext`; they deliberately do not call `SaveChangesAsync`. A domain operation that
  enqueues notifications persists them with its own unit of work, so enqueueing cannot flush
  unrelated tracked changes at an unexpected point.

The service has Postgres models; they are discovered because the API references this project.
Firebase fills the push port (`IPushSender`) and references only Notification's contracts.
Notification does not reference Cache — it has nothing to cache.

## Deliberately not built

No admin send/broadcast action, inbox/history endpoint, per-kind preference, digest, quiet hours,
SMS, APNs, web push, native Windows/macOS toast, FCM topic, or delivery report is part of this
service.
