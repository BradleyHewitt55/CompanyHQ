# Web Project

The account editor accepts a saved role name as text. A new name is created by the API with Client
permissions; an existing name assigns the shared row. MCP checkboxes on that form only narrow an
account's capabilities. Shared grants are edited under Settings, Roles (`/api/access-roles`); the
permission labels there are `src/api/permissions.ts` and must name what `AccessPermissions.All` in
the API defines.

Never gate on `role === 'Admin'`. Ask `resourceFor(key, user.permissions)` for a resource list,
`requirePermission(...)` in a server action or page, and add a new area's permission to
`areaPermissions` so `canOpenAdmin` admits a role that holds only that.

The Inbox lists every channel `GET /api/inbox/channels` reports for the signed-in role, one after the
other; a record id is `channel.id` (`src/api/inbox.ts`). A new channel plugged into the API shows up
with no change here. List search boxes send `?keyword=`, the API's shared search.

Blog cover uploads use Media's public Content scope. New uploads are completed before saving
the blog, and deleted if that save fails. Blog owns retirement of recorded old cover ids; Web must
never guess a media id from a URL and delete it. Blog bodies are HTML passed through the API; a
consumer must sanitize them before rendering. The admin detail continues to display source text.

Company's first two public mailbox entries supply Contact's support and information addresses;
additional titled entries are published on the home page. Failed saves retain the mailbox fields.

The public home page shows the contact form only for what `GET /api/widget` reports as available,
and posts it through `app/widget-actions.ts` with the source `Website`.

Deleting your own account is two steps: Settings, User Profile takes the typed name and asks the API
to mail a link; `/auth/confirm-account-deletion` redeems it from the browser once the page has
loaded. That page is public in `middleware.ts` because the link is often opened signed out.
