# Blog service

`Services/Blog` owns CompanyHQ-authored blog content. It stores a stable lowercase slug, the
search description, HTML article body, author, required free-text category, optional social
image, publication date, editorial order, and draft state in Postgres.

Anonymous reads expose only posts that are not drafts with a publication date at or before now.
Future-dated posts stay hidden until their time arrives. A non-draft write without `PublishedAt`
is a 400. The list is ordered by newest publication date, then editorial order and id; detail reads
use the slug and return 404 for missing or unpublished content. Role permissions for the specific
list, read, create, update, and delete actions govern draft visibility and authoring. Those rules are
rechecked in `BlogService` for both HTTP and MCP. Slugs remain immutable after creation.

`GET /api/blog?field=&keyword=` is the shared search (`Postgres/AGENTS.md`): the keyword is a
case-insensitive contains match, in SQL, against the named field or every text field of the shape
the caller receives. A reader therefore cannot search `isDraft`; a role with the list permission
can. It runs alongside the draft filter, so an anonymous search still sees published posts only.
The admin console relies on it: never move that filter into a consumer.

The service returns `ClientBlogPostDto` / `AdminBlogPostDto`, never `BlogPost`. Projections filter,
order, page-limit, and project in SQL. `Description` is the SEO description; `Body` is author-authored HTML, stored and returned without encoding. Every consuming renderer
must sanitize it with an allowlist before inserting it into HTML; author permissions do not make
script, event handlers, or unsafe links safe to render. Cover
URLs are stored metadata and are never fetched by the service. Managed covers carry an optional
media id verified through `IBlogCoverImages`; only ready Content images owned by the uploading
author can be newly attached. Keeping an existing id keeps its stored URL, including when another
editor updates the post. External cover URLs remain supported. After replacement or deletion,
the last blog reference retires its managed image through the host bridge. A failed save leaves the
old cover untouched; the uploading consumer deletes a new upload after a definite rejection.
An ambiguous network/server failure must retain the upload because the write may have committed.
Retirement is best effort after the post write commits; cleanup failure cannot fail the saved post.
`Category` is free text (2 to 100
characters), never an enum: the published set is what Marketing turns into its filter row.

Marketing consumes the anonymous API at build time and prerenders `/blog` and `/blog/{slug}`. The
API is the source of truth; local markdown is not a fallback content store. Draft exclusion is
therefore enforced twice: in this service and by the static route generation receiving only the
published response.

A blog write that changes the shape or publication rules must update the OpenAPI document, the
Marketing generated aliases, and `Blog.http`. The unique slug index is the
idempotency boundary for seed data; do not replace it with a mutable title lookup.

## Runtime catalog constraints

Runtime disable revokes public reads, authoring, direct service calls and MCP discovery/direct calls. The named blogs.manage aggregate grants HTTP/domain authoring and draft access alongside the existing individual grants; it does not bypass MCP action permissions, read-only keys or deny lists.
