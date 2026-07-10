# SEO

> **Document status:** Target V1 product contract.

SEO is a core CMS capability that produces policy-safe metadata for each project route and locale. It does not allow content editors to inject arbitrary document markup or executable code.

## SEO record

The SEO contract supports title, description, canonical route, indexing/follow controls, social title/description/image, content type, locale alternates, structured-data references, sitemap inclusion, priority/change hints, and redirect history. Defaults can be defined at company, project, content-type, and entry level with a deterministic override order.

~~~text
entry and locale value -> content-type default -> project default -> company default
~~~

The final project DTO contains already validated values. Templates escape text and render only registered metadata fields.

## Canonical and locale rules

Canonical URLs are built from the approved project origin and route registry, never directly from an inbound host header. Locale alternates include only published, authorized translations. Draft and preview pages emit non-indexing controls and never enter public sitemaps.

Slug changes create an explicit redirect proposal. Redirects validate ownership, loops, collisions, reserved routes, and status policy before activation. Redirect chains are collapsed through a governed job.

## Structured data

Structured data uses registered schemas mapped from DTO fields. Editors select an allowed type and values; they cannot store arbitrary scripts. Validation rejects unknown properties, unsafe URLs, hidden data, and relationships outside the published project scope.

## Sitemap and robots flow

Publication and unpublication emit idempotent jobs that update the project's sitemap index and route entries. Sitemap generation includes only published, indexable routes for the correct origin and locale. Robots policy is project-owned, versioned, previewable, and protected from accidental production-wide blocking.

## Tests

Tests cover fallback order, escaping, canonical origin, locale alternates, preview exclusion, sitemap publication/unpublication, redirect loops/collisions, structured-data validation, DTO field omission, tenant isolation, and cache invalidation.
