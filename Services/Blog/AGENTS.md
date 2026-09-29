# Blog service

`Services/Blog` owns CompanyHQ-authored blog content. It stores a stable lowercase slug, the
search description, plain-text article body, author, required free-text category, optional social
image, publication date, editorial order, and draft state in Postgres.

Anonymous reads expose only posts that are not drafts with a publication date at or before now.
Future-dated posts stay hidden until their time arrives. A non-draft write without `PublishedAt`
is a 400. The list is ordered by newest publication date, then editorial order and id; detail reads
use the slug and return 404 for missing or unpublished content. Role permissions for the specific
list, read, create, update, and delete actions govern draft visibility and authoring. Those rules are
rechecked in `BlogService` for both HTTP and MCP. Slugs remain immutable after creation.

`GET /api/blog?search=` narrows on title, description, author, and category with a case-insensitive
contains match, in SQL. It runs before the draft filter, so an anonymous search still sees published
posts only. The admin console relies on it: never move that filter into a consumer.

The service returns `ClientBlogPostDto` / `AdminBlogPostDto`, never `BlogPost`. Projections filter,
order, page-limit, and project in SQL. `Description` is the SEO description; `Body` is deliberately
plain text with paragraph breaks because Marketing escapes it while rendering static HTML. Cover
URLs are stored metadata and are never fetched by the service. `Category` is free text (2 to 100
characters), never an enum: the published set is what Marketing turns into its filter row.

Marketing consumes the anonymous API at build time and prerenders `/blog` and `/blog/{slug}`. The
API is the source of truth; local markdown is not a fallback content store. Draft exclusion is
therefore enforced twice: in this service and by the static route generation receiving only the
published response.

A blog write that changes the shape or publication rules must update the OpenAPI document, the
Marketing generated aliases, and `Blog.http`. The unique slug index is the
idempotency boundary for seed data; do not replace it with a mutable title lookup.
