# FAQ service

`Services/Faq` owns CompanyHQ's frequently asked questions. Each entry has an immutable lowercase
key, question, plain-text answer, category, SEO description, editorial order, publication time,
and draft state. The key is the route identity and the seed natural key.

Anonymous reads expose only entries that are published and not drafts, ordered by editorial order,
category, and key. List paging is one-based with `pageSize` clamped to 1 to 100 server-side. A single
key read returns the published entry or `404`. `GET /api/faqs?field=&keyword=` is the shared
search (`Postgres/AGENTS.md`) over the fields of the caller's shape, in SQL, alongside the
publication filter. Each write
requires its saved role permission in both the HTTP policy and `FaqService`; draft reads require the
matching list or get permission. A published entry must
carry `PublishedAt`, invalid content is `400` and a duplicate key is `409`. The service projects
`ClientFaqEntryDto` / `AdminFaqEntryDto` in SQL and never returns persistence entities.

Marketing fetches the public list at build time and renders the answer text and the matching FAQPage
JSON-LD from the same generated DTO values. This service is the only content source; local markdown
is not used as a fallback. The API-side draft filter remains authoritative even if another consumer
forgets its own filter.

Keep the unique key index, publication filter, and SQL ordering intact. Changes to the contract must
regenerate `Services/Faq/Contracts/OpenApi/faq.json` and the consuming app's
`src/api/schema.d.ts` with `npm run api:sync`, and update `Faq.http`.

## Runtime catalog constraints

Catalog admission covers HTTP and direct service operations, and MCP filters both listing and direct calls by current feature enablement without weakening the individual action permissions.
