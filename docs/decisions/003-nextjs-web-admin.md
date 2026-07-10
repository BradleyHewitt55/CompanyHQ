# ADR-003: One Next.js public/admin application

> **Status:** Accepted. **Review:** 2027-01-15.

## Context and decision
Each company needs a website at `/` and admin at `/admin` on one deployment. Use one Next.js app with separate route groups/layouts and shared application clients.

## Alternatives considered
Separate site/admin deployments; SPA admin plus API; multiple Next.js apps.

## Advantages, disadvantages, and risks
One release simplifies routing, preview, auth, and provisioning. Public/admin coupling increases blast radius; route groups, bundles, CSP, auth boundaries, and tests keep separation explicit.

## Consequences and migration
Admin internals cannot enter public bundles. A future independently scaled admin requires a new deployment ADR, not a casual split.
