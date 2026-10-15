# Shipping

## Ownership

Owns delivery methods, postal zones, fulfillment origins, exact route planning and public quotes.
Catalog facts, coupons and address/geographic providers arrive through narrow optional ports;
Shipping never references Sales, Geo or their Contracts.

## Rules

- Use active methods with product allow-lists. Candidate origins are supplier origins OR active
  method-linked origins. An invalid origin must not invalidate a usable alternative.
- No active postal zones means unrestricted coverage. Active zones require a matching exact,
  inclusive numeric range or wildcard rule. Distance is Haversine, inclusive at the maximum;
  constrained methods reject unresolved origins/destinations.
- Charge each unique method-plus-origin once across products. Quantity and weight never multiply
  shipping cost. Plan globally, with deterministic method/origin tie-breaking.
- Search remains bounded. If optimality cannot be proven within the budget, return unavailable
  with complexity_limit; never return or charge the best approximate plan found so far.
- Discount policy receives server routes and basket facts, clamps discounts to the base cost and
  reports only codes that actually reduced cost. Sales' policy prioritizes free, percentage, fixed.
- Enforce shipping.manage at HTTP and in-process mutation boundaries. Persist zone replacement
  and origin links atomically, and cascade owned rows when deleting methods/origins.

## Decisions

The default search budget is 200,000 steps; at most 200 active methods, 2,000 unique candidate legs
and 10,000 candidate entries are supported. Input bounds are enforced for direct callers as well
as MVC. Complexity failure is a typed quote failure, not permission to use an approximate plan.
Without catalog integration, supplied product ids are opaque and only method origins are usable.
Without geocoding, callers must supply both coordinates for distance constraints. Without coupons,
quotes are undiscounted. Without an address directory, search/hydration return explicit unavailability.
There is no carrier booking, tracking, weight surcharge or fixed company geography.

## Traps and limitations

Origins must retain stable identities so shared charging remains meaningful. Host bridges carry
no pricing rules. Geographic candidates advertise their precision: HQ's Geo adapter supplies area
candidates, not verified street/postal addresses. Companies needing that accuracy replace the
address directory/geocoder. Coordinates and monetary facts returned by ports must be finite and
valid. Route distances preserve full precision for later payment-method/radius decisions.
Malformed provider coordinates (nonfinite, out of range or partial pairs) are rejected at origin
and geocoder boundaries. A bad origin never hides a valid alternative. Basket context passed to
discount ports retains identities, selections and parent links; its strings and collections are
bounded before invoking the port. Standalone basket ids may be omitted unless parent links are used.

## Runtime catalog constraints

Catalog admission covers concrete address operations and domain calls as well as HTTP. A present bridge whose Sales or Geo target is disabled/unconfigured reports typed dependency unavailability; it must not masquerade as missing products, empty address results or an undiscounted successful quote.
