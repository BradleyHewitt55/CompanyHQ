# Geo

Owns area naming/search and nearby-place domain policy, rounded privacy coordinates, tiles/cache/SWR,
public routes/DTOs, limits and the provider-neutral port. OpenStreetMap owns HTTP/parsing/configuration
and upstream pacing; Geo owns no entity, location history, database or provider implementation.

## Rules

- No provider client/HTTP/raw response/OSM parsing or Geo -> OpenStreetMap dependency. Other providers
  cross Geo.Contracts only. Provider results are neutral and filtered against configured kinds/bounds.
- Round device coordinates to three decimals before any provider request/cache key. Never persist/log
  a precise device coordinate. Business pin positions are published map data, not device positions.
- Preserve anonymous Geo HTTP access, exact DTO/error/rate-limit behavior and the MCP area's separate
  search_area permission. Changing transport must not change public contracts or cache versions.
- Area successes alone are cached. Provider outages are null/empty failed answers, not retry signals.
  Only caller cancellation propagates from adapters. Do not cache failed tiles or retry upstreams.
- Preserve fixed slippy zoom-12 grid, v3 tile namespace/payload, kinds sorted/joined in keys, and existing
  area key inputs. A new field/meaning needs deliberate cache-version handling, not an adapter swap.
- Empty tiles are valid cached answers; failed tiles are absent. Fill missing tiles with one enclosing
  bounded box query and cache every tile scanned (including intervening and boundary tiles).
- Assign each place to one tile by its own center. Never duplicate way/relation pins across tiles.
- Preserve the 24-tile viewport cost cap and reject invalid/antimeridian boxes. Round viewport bounds
  outward so they never collapse or drop a requested edge. Query boxes, never compositional radii.
- Soft TTL is in FetchedAt; the longer hard TTL is cache expiry. Reject hard TTL below soft TTL.
  Serve stale tiles immediately, refresh in an independent scope without request cancellation, and
  swallow background failure. Do not turn SWR into request-path fetch-every-time or push updates.
- Only empty-plus-failed resolves to unavailable. Partial cached data survives outages with Incomplete;
  return only resolved tiles so clients retry genuinely missing ones, not cache failures as empty.

## Decisions and limits

No provider fallback, second endpoint form, precise address geocoding, routing, opening hours, place
identity, location persistence or new transport API. Phone/address are optional published business
fields; clients hide missing actions rather than fabricate them. Kind-derived fallback names remain
Geo policy. Libraries without new HTTP endpoints need no provider OpenAPI document.

## Traps

The adapter binds the same Geo section so existing deployments keep configuration. GeoOptions keeps
former transport properties as obsolete source shims but never reads them; adapters own validation.
Public concrete constructors/transport namespaces changed and require a coordinated major upgrade
(see Docs/provider-upgrade.md), while IGeoService/HTTP DTOs remain stable. Tile keys/payloads must
remain readable through a provider split. Always test using a fake neutral provider and scripted
HTTP; never contact public Nominatim/Overpass in a test.

## Runtime catalog constraints

Catalog admission gates cached domain results and provider operations as well as HTTP/MCP. Explicit unavailable providers throw typed unavailability, never successful empty data. Detached refresh uses host shutdown plus a bounded timeout, rechecks admission and cannot continue unchecked with CancellationToken.None.
