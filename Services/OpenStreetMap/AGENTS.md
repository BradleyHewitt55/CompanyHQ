# OpenStreetMap

Owns Nominatim/Overpass HTTP, raw responses, parsing, endpoint configuration, identity and pacing.
Geo owns domain policy, rounded privacy coordinates, cache/tiles, public routes and DTOs. This adapter
references Geo.Contracts and shared ServiceCatalog.Contracts, never Geo or Cache implementations.

## Rules

- Bind the existing Geo section: deployments must not rename connection/identity settings to upgrade.
- No live public endpoint in tests. Script HTTP responses, including failures and invariant-culture
  coordinates, and retain a singleton pacer for each upstream.
- Never retry upstream failures or log search text/coordinates. Only caller cancellation propagates;
  preserve port distinctions between failed and empty results.
- Always send an identifying contact User-Agent. Measure pacing from the end of the previous call.
- Validate amenity identifiers before query interpolation. One bounded regex query, not one scan per
  kind; transport timeout must exceed the upstream query budget.
- The optional catalog HTTP handler rechecks committed Geo admission after provider pacing and before
  outbound HTTP. Legacy standalone registrations without availability retain their strict behavior.

## Decisions and limits

Keep both upstreams in one adapter. No SDK, fallback instance, routing, OSM writes or new endpoints.
The old transport types remain public here for direct adapter consumers, but their namespace and
assembly moved: this is not a transparent binary upgrade (see Docs/provider-upgrade.md).

## Traps

Nominatim coordinates are strings; parse invariantly. Ways/relations need their center. Missing
position or amenity is not a place. addr:full wins over composed parts and the first phone wins,
with contact:phone as fallback. Language and zoom come from Geo on each request; duplicating their
configuration here could make a cached answer disagree with its key.
