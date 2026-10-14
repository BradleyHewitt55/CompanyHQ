# Widget service

`Services/Widget` (`CompanyHQ.Widget`) is the public front door for a contact widget on a
site or in an app: it opens visitor conversations and files bug reports. It owns the anonymous
routes, their rate limits, and the list of sources a submission may come from. It stores nothing
and does not own the conversations (that is `Inbox`) or the issue repository (that is `GitHub`).

## Rules a change must not break

- **It references no other service.** Its two destinations are ports in `Contracts/Ports/`:
  `IWidgetConversations` and `IWidgetIssueTracker`, both filled in
  `Apps/Api/Integrations/WidgetIntegration.cs`. Both are optional; without one, that half answers
  503 and `GET /api/widget` reports it as off.
- **Every submission carries where it came from.** `Source` must be one of `Widget:Sources`
  (matched without case, stored in the configured spelling); it defaults to the first when left
  out, and anything else is a 400. `Origin` is the free-text page or screen. Both are passed to the
  destination, which keeps them.
- **Everything in a request is anonymous public input.** The email is where a reply is mailed or
  what is echoed into a report; it is never an identity.
- **A conversation is proved by its token**, returned once by `POST /api/widget/conversations` and
  sent back in a request body, never a URL. A wrong token and an unknown id are the same 404.
- The two rate limits (`widget-submit`, five per 15 minutes; `widget-chat`, 60 per minute, both per
  address) are spam brakes, not authorization, and are registered by `AddWidget`.

## Decisions, and what was not built

- **Sources are configuration, not a table.** A deployment lists them in `Widget:Sources`
  (`Widget__Sources__0=Website`, `Widget__Sources__1=App`). The default is `Website` alone.
- **The source is a label, not a credential.** It says which of the company's own surfaces a
  submission came through; it does not authenticate the sender. Browser origins are restricted by
  the host's CORS policy, not here.
- **Issue categories are fixed** (`Bug`, `Feature`, `Security`, `Other`) and reported by
  `GET /api/widget` so a UI does not hard-code them.
- No staff routes: staff answer in the Inbox. No attachments, no captcha, no visitor accounts.

## Traps and wiring

- `AddWidget(configuration)` validates `Widget:Sources` at startup; an empty list stops the host.
- A new destination (a second tracker, a CRM) is a new port here and a bridge in the host, never a
  reference from this project.
