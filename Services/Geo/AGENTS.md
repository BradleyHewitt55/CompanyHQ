# Geo service

Turns rounded coordinates into named areas, searches areas by name, and finds nearby veterinary practices and animal shelters. It owns no entity, table, migration, or location history.

## Invariants

- **Precise device coordinates never reach this service.** The client rounds before sending and `GeoService` rounds to 3 decimals again before a provider call or cache key.
- No coordinate appears in a database, log message, or response more precisely than the rounded pair.
- Every endpoint is anonymous. A signed-out user still needs a map.
- The `areas` action is explicitly marked for MCP area search. MCP requires the role's `search_area`
  permission even though the HTTP location picker remains anonymous.
- Area lookup with invalid coordinates is `400` and an unavailable Nominatim result is `404`. Area
  search requires `search` (`400` when missing) and clamps `limit` to 1 to 20.
- Nominatim is used only for area naming/search. Overpass is used only for the tiled bounding-box search of `amenity=veterinary` and `amenity=animal_shelter`.
- `NominatimClient` and `OverpassClient` own their HTTP calls. Call them only through `IGeoService`, which applies rounding and caching.
- Both providers are individually serialised by their singleton rate limiter and share the required identifying `Geo:UserAgent`.
- Provider failures degrade to `null` or empty results and log no coordinates. Never retry a failed public-provider request.
- Geo references no Postgres project and declares no `BaseModel`, so model discovery never maps anything here. Server-owned locations belong to their owning Postgres service, not here.
- The inbound limit is `GeoRateLimitPolicy`, a policy class rather than a lambda because it owns the Geo-specific 429 body. Never move that body to the shared `RateLimiterOptions.OnRejected` — it would answer every other service's rejections as a Geo one.

## Map contract

`GET /api/geo/nearby-places` takes a **viewport** — `south`, `west`, `north`, `east`. The lat/lon
form is gone, not kept as a default: there is exactly one client, and a second differently-capped
entry point that nothing calls is a liability, not compatibility. It returns `NearbyPlacesResultDto`
— the `NearbyPlaceDto` pins, the `Tiles` the server actually resolved, and `Incomplete`. A pin is
position, name, kind, and the optional `Address` and `Phone` the pin sheet needs for its
"Directions" and "Call" actions. No cards, no routing, no opening hours, no place ids.

`Address` and `Phone` come straight from the OpenStreetMap tags (`addr:*`/`addr:full`, `phone` falling back to `contact:phone`) and are null far more often than not — OSM tagging is uneven. **The client must hide an action it has no value for, never disable it.** These are published business details, not a person's; the address rule in `Services/AGENTS.md` is about user contact details and does not apply. Nothing here weakens the coordinate-rounding invariant above, which is about the *user's* position.

### The tile cache

One Redis entry per fixed-grid tile, keyed `geo:nearby-tile:v1:{z}:{x}:{y}` at **slippy zoom 12** —
8.8 km square at mid latitudes, 9.8 km at the equator. The old key was the caller's centre point
(`geo:nearby-places:v2:15:{lat}:{lon}`), which meant a 110 m pan missed and a 5 km result could
never satisfy a 15 km one. **Nested radii do not compose; an absolute grid does.** The `v1` marker
carries the same duty the old `v2` did — bump it whenever `NearbyPlaceDto` or `NearbyTileDto` gains
or loses a field.

- A request resolves to the tiles covering its viewport. Cached ones are served; the missing ones are
  filled by **one** Overpass bbox query over their enclosing rectangle, split back into one entry per
  tile. Every tile inside that rectangle is cached, including ones that were only in the way — the
  query scanned them too.
- A place belongs to the single tile its **own centre** falls in. Tiles are disjoint, so a way whose
  `center` sits near a boundary is stored in exactly one place and can never be returned twice.
- An **empty tile is a real, cacheable answer.** A failed one is not written at all. That invariant
  is the reason this feature exists and nothing may weaken it.
- The query is one regex pass (`nwr["amenity"~"^(veterinary|animal_shelter)$"]`), not a scan per
  amenity. Two `around` scans measured 18.6s against 8.2s for the identical elements, and
  overpass-api.de culls long queries first — that was what produced the 504s and 429s behind the
  map's error banner.

### Stale-while-revalidate

`NearbyPlaceCacheTtlHours` (6) is now the **soft** TTL and lives in the entry's `FetchedAt`;
`NearbyTileHardTtlHours` (72) is what Redis is actually given. Past the soft TTL a tile is still
served and `NearbyTileRefresher` starts a background refresh.

**This is for outage resilience, not freshness.** A veterinary practice does not move; a six-hour-old
pin is correct. What matters is that the map keeps working while the public Overpass endpoint is
rate-limiting, and it only can if a stale entry is served instead of being discarded and refetched on
the request path. The refresh runs in its own DI scope on `CancellationToken.None` — the request's
token is about to be cancelled — and swallows everything. **Nothing is pushed to the live user**: no
SSE, no polling. They pick it up on their next pan.

### The cost cap

The 15 km radius used to be server-owned so a client could not enlarge an expensive public-provider
query. A caller-supplied viewport hands that control back, so `GeoViewport.MaxTiles` replaces it:
**more than 24 tiles is a `400`**, as is a box that is inverted, out of range, or crosses the
antimeridian. 24 sits just above the 20 tiles the old fixed 15 km radius needed, so nothing that
worked before is refused; a fully cold fetch is then about 35 km × 53 km. The box is rounded
*outward* to 3 decimals so it can never collapse and never returns less than was asked for.

### When it is still a 503

Only when the result would be **empty and at least one tile genuinely failed**. If Overpass is down
but some tiles are cached, those are returned with `Incomplete: true` rather than an error — partial
data beats a banner. A client marks only the returned tiles as held, so a failed tile is asked for
again on the next pan. A real outage in an area nothing is cached for stays distinguishable from "this
area has no vets", which is the one thing this feature must never get wrong.

## Configuration

`Geo:BaseAddress` configures Nominatim; `Geo:OverpassBaseAddress` configures Overpass. `Geo:UserAgent` (`Geo__UserAgent`) must carry reachable contact information. `Geo:CacheTtlHours` is for areas; `Geo:NearbyPlaceCacheTtlHours` is the nearby-tile **soft** TTL and `Geo:NearbyTileHardTtlHours` the hard one. A hard TTL below the soft one throws at startup — it would evict every tile before it was ever stale and quietly turn stale-while-revalidate back into fetch-every-request.

Full reference: `docs/Services/Geo.md`.
