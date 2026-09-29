# Message service

`Services/Message` owns two-party text conversations, immutable message history, read cursors, and
the realtime delivery adapter. It is deliberately a participant-scoped service: a caller can read
only a conversation containing their own account, and an operator can read a flagged person's
thread only when that operator is one of the two participants.

## Rules

- `Conversation.ParticipantKey` is the sorted pair of user ids and has a database-enforced unique
  index. Get-or-create must keep that invariant under concurrent starts.
- `ConversationParticipant` is one row per side. `LastMessageAt` belongs there, not on
  `Conversation`, because a suppressed message must reorder only the sender's list. `LastReadAt`,
  `ClearedAt`, and `MutedAt` are also per side.
- Conversation lists keep `ConversationParticipant` as the query root through caller filtering,
  ordering, counting, and paging; only the page is projected into `ClientConversationDto`. Applying
  list predicates or ordering to that DTO projection does not translate in EF Core and makes every
  conversation-list request fail.
- A non-participant gets 404 on every conversation route. The service does not confirm that a
  conversation exists to someone who cannot read it. **The send path must resolve the caller's own
  participant row before it picks "the other side."** Both rows satisfy `UserId != caller` for an
  outsider, so a `SingleOrDefault` on that predicate throws — and a 500 on a real conversation id
  beside a 404 on a fabricated one is exactly the existence oracle this rule forbids.
- Sending is an HTTP operation. The hub only joins per-user groups and delivers events; it never
  writes Postgres. A blocked or spam-suppressed send is still stored and returns 201, but it has no
  suppression field on the wire.
- Send is idempotent by (SenderUserId, ClientMessageId): a retry returns the stored message, reuse of one id across conversations is Conflict.
- Suppression is enforced in one shared message queryable. The recipient path excludes
  `SuppressedAt` rows and globally `ActionTaken` report ids; the sender path includes its own rows.
  The registered Message media policy repeats the global report-id check before issuing an attachment
  URL. Suppressed rows are never deleted, counted as unread, broadcast, notified, or used to update
  the recipient's `LastMessageAt`. Unread counts and previews also exclude rows at or before ClearedAt. Admin participant reads remain available for operations.
- A flagged sender may still message the Admin account, and messages sent to a flagged account are
  not suppressed. Clearing a flag affects new sends only; rows suppressed while it was active stay
  suppressed.
- Message knows nothing about reporting. It exposes `MessageContextReader` (a message with two
  neighbours each side) and `MessageHistoryClearer`; the host turns them into a reporting service's
  source and clearer. Evidence is read before a caller's clear cursor is applied.
- Thread paging is a `(CreatedAt, Id)` descending keyset cursor. Offset paging is intentionally not
  used because active conversations shift while a reader is paging.
- Participant search is bounded to one conversation. The one exception is the administrator's
  closed-conversation list, where `?search=` runs an `ILIKE` over message bodies and participant
  first and last names inside the already `ClosedAt != null` set: the admin console pages that list
  server-side and cannot filter a loaded page honestly. `ponytail:` it is an unindexed scan over
  closed threads. Once that set stops being small, give it a full-text vector and a GIN index
  rather than widening the pattern.
- Spam detection uses the shared cache counter abstraction, raises `message.spam`, and notifies the
  operator without banning, deleting, or emailing the account. Its thresholds are configuration
  values so operations can tune them without a code change.
- `IMessageNotifier` is optional: with no bridge, nothing is sent. A bridge must add to the shared
  unit of work and not save — Message's own `SaveChangesAsync` commits the queued rows with the
  change that caused them.
- `Message:Spam` uses a rolling volume limit of 100 and a distinct-new-conversation limit of 20 in
  a 60-minute window by default. Cache unavailability fails closed for ordinary recipients; the
  Admin and flagged-recipient carve-outs still apply.
- `POST /api/conversations/with/{userId}` is the only operator conversation entry point. There is
  intentionally no arbitrary admin live-thread reader. A participant can close a conversation, after
  which administrators may review its history through the closed-only admin endpoints.
- `POST /api/conversations` is get-or-create with an existing account by `ParticipantId`. The caller's
  own id is 400 and an unknown id is 404. There is no user search here; a caller must already know
  the id.
- `POST /api/conversations/{id}/close` closes both participant views permanently. Closed threads are
  read-only for participants, while every administrator is notified and may review the complete history.

## Attachments

A `Message` may carry one optional `MediaObjectId`, an id owned by whatever media service the host
plugs in. Sending asks `IMessageAttachments.IsReadyAsync` before the message is written; the object
must be ready and belong to the sender, or the send is invalid (400). With no attachment bridge every
attachment is invalid.

Reading an attachment does not go through Message; the client calls the media service directly, and
the host's bridge hands that service `MessageAttachmentAccess` as its read policy. It asks
the same question the thread-read path asks: is the viewer a participant on the attachment's message,
and if that message is suppressed, is the viewer its sender? Suppression therefore applies to the
attachment exactly as it applies to the message body — a suppressed message's attachment is
unreadable to its recipient through the presigned URL, even though the row itself is never deleted.
Losing that check would open a side channel: a blocked sender's suppressed message is invisible in
the thread, but its attachment would still resolve for anyone who could guess or retain the media id.
Keep the restriction check in `MessageAttachmentAccess` and the thread query in step.

An administrator reads an attachment on exactly the terms they read the text: only once the
conversation is closed, and never in a live one. Media has no admin bypass, so this rule is the only
thing that grants it.

## Wiring

`AddMessages()` registers SignalR and `SignalRMessageBroadcaster` itself, and owns the
`MessageSends` rate-limit policy (60 per minute, per authenticated `sub`). The host only calls
`app.MapMessages()`, which mounts `MessageHub` at `MessageHub.Path` (`/hubs/messages`). The hub sits
under `AuthHubs.PathPrefix` on purpose: that is where Auth accepts the browser's `access_token` query
parameter, so moving it elsewhere silently makes every socket anonymous and `[Authorize]` rejects it.
Message references Auth for that prefix and `AuthClaims.Subject`; Auth never references Message.

Message references only foundation (`Postgres`, `User`, `Auth/Contracts`, `Cache`). Blocking, flags,
report restrictions, notifications, and attachments are its own optional ports in
`Contracts/Ports/` (`IMessageBlocks`, `IMessageAccountFlags`, `IMessageRestrictions`,
`IMessageNotifier`, `IMessageAttachments`). `Apps/Api/Integrations/MessageIntegration.cs` bridges
them to Moderation, Notification and Media. Remove a bridge and that capability switches off; Message
still builds and runs.

`Message:Spam` binds once at startup and is validated there; a bad threshold stops the host.

There is no null broadcaster. The API is the only host, and it always has the hub.

## Boundaries

This service does not build groups, typing indicators, presence, reactions, edits, deletes, unsends,
location sharing, push transport, or an administrator's arbitrary live-conversation read route.
Administrators can review explicitly closed conversation history only; live reads remain participant-scoped.
