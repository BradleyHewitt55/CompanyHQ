# Inbox service

`Services/Inbox` (`CompanyHQ.Inbox`) is the staff inbox: one set of routes over every
channel a deployment plugs in, and the store for conversations opened by a visitor who has no
account. It does not own the public form that collects those conversations (that is `Widget`),
member-to-member conversations (that is `Message`), or issues (that is `GitHub`).

## Rules a change must not break

- **A channel is a port, never a reference.** `IInboxChannel` in `Contracts/Ports/` is everything
  Inbox knows about a source. Inbox references no feature service; a service's threads appear here
  because the host registers a bridge in `Apps/Api/Integrations/InboxIntegration.cs`. Adding a
  social account later is one more `IInboxChannel` and one more line there.
- **Two permissions gate every call.** `inbox.manage` opens the inbox (policy on the controller,
  rechecked in `InboxService`), and each channel names its own `Permission` on top
  (`github.manage`, `conversations.manage`). A role without a channel's permission does not see it
  in `GET /api/inbox/channels` and gets 403 on its routes.
- **Channel keys are stable, lowercase and URL-safe.** They are the `{channel}` route segment and
  part of every link the console stores. The built-in one is `messages`.
- **A thread id is the channel's own, as text.** Inbox never parses it; the channel does, and
  answers `NotFound` for one it cannot read.
- **The visitor's only credential is the conversation token.** `IVisitorConversationService` stores
  it only as a SHA-256 hash; a wrong token and an unknown id are the same answer. That interface has
  no HTTP route here: `Widget` reaches it through a bridge and owns the rate limits.
- A resolved conversation takes no further messages from either side. Resolving is one-way.
- Resolved conversations stop being readable 30 days after they were resolved, whether or not the
  hourly purge has run yet, and are then deleted with their replies. `DELETE` removes one at once.

## Decisions, and what was not built

- **The list is per channel, not merged.** External channels page on their own backend, so a merged
  page with a true total is not possible. The console walks the channels in order.
- **What a channel cannot do is `Unsupported`, and the thread says so up front** with `CanReply`,
  `CanResolve` and `CanDelete`, so a UI never offers a button that answers 400.
- **Search is the shared `?field=&keyword=`.** The built-in channel honours both, over the fields
  of `AdminInboxItemDto`; an external channel honours the keyword only.
- **Where a conversation came from is two columns:** `Source` (which of the company's surfaces, as
  the collecting service named it) and `Origin` (the page or screen). Both are shown to staff.
- **Mailing a reply is an optional port.** `IInboxReplyMailer` is filled from Email's `inbox.reply`
  template. Without the bridge a reply is still stored and the visitor reads it in the widget.
- No attachments, no assignment between staff, no reopening, no push to staff on a new message.
  The admin console polls the built-in channel only.

## Traps and wiring

- `VisitorConversationService` is one scoped instance registered three ways (itself,
  `IVisitorConversationService`, `IInboxChannel`). It takes the neutral `IPersistence` unit: a reply and the
  conversation's activity stamp are one save.
- A reply is saved before it is mailed, and a mail that fails to queue is logged by the bridge, not
  surfaced: the stored reply is the source of truth.
- A channel bridge must not depend on anything that depends back on Inbox. The host builds every
  channel to answer any inbox call.
