# Sales

## Ownership

Owns the generic catalog, recursive pricing, promotions, browser-cart normalization, orders,
upsells and staff quotes. Shipping, customer profiles, geography, payments and email remain host
capabilities; Sales never references another feature or its Contracts.

## Rules

- All prices come from persisted cost and markup. Currency uses AwayFromZero; variant rounding
  overrides product rounding. Configured matched-variant multipliers derive retail from the matching baseline variant;
  extra-length terminology and factors belong to company configuration.
- Bundles select mapped options, configured fallback, then first variant; recurse discounts per
  component before quantity. Reject cycles, excessive depth and mixed currencies. Catalog edits
  validate inbound ancestors in the same serializable transaction; recursive pricing also checks
  currency on every edge so inconsistent stored graphs cannot silently price across currencies.
- Coupon dates are inclusive business-calendar dates, default UTC; companies select their timezone. Product and category
  targeting is OR. Automatic bundle/stackable discounts compete against the best single discount;
  clamp at zero and snapshot only discounts actually used. Checkout subtracts every discount.
- Cart identity includes canonical variant/options, bundle instance and parent line. Merge parents
  before rewriting child references and recursively remove orphan descendants. Never trust cart prices.
- Self-service cannot supply tax, manual discounts, shipping amounts, customer ids or saved address
  ids. Signed-in customer details resolve through the customer port. Guest order credentials are
  random, returned once, hashed at rest, expiring, and accepted only for guest-owned orders.
- Order numbers are serialized by a locked counter. Initial pricing, cost, coupon, customer,
  address and method/route snapshots are retained unchanged. Upsell additions retain separate
  snapshots, with locked order mutations and persistent request/offer uniqueness.
- Offers originate from nonupsell parents, exclude already-added products, synchronize common
  option names/values without case, and suppress incompatible resolved choices. Absent choices
  fall back. Accepted offer ids are suppressed for the order's lifetime, including after staff
  repricing removes an upsell; historical request replay still returns its original receipt.
  Automatic, relationship and company percentages add against selling price.
- Quotation conversion copies the accepted immutable pricing and route snapshot, retains the original quotation, and creates at most
  one unpaid order. Only drafts can be replaced/deleted; issued documents and orders are retained.
- Reserve a quotation's exact draft under a row lock before provider delivery. Issuing freezes the
  content even after rejection or timeout; retries retain its original provider key. Acceptance
  requires provider-accepted issuing or an explicit staff-recorded send transition. Queue success
  leaves it Issuing; repeating send observes later provider acceptance before marking it Sent.
- Invoice request ids bind permanently to a customer-safe document snapshot and provider key,
  reserved under the order mutation lock before external work. Repricing, additions, recipient
  or invoice-reference changes cannot alter retries; a fresh request snapshots the current order.
  Queued and accepted timestamps record distinct observations, never proof of inbox delivery.
- Upsell acceptance rechecks the resulting total and every route against the active payment
  method before mutating the order. Shipping receives canonical line identities, options, bundle
  instances and upsell parents so its discount policy prices the same basket.
- Product option ids and dependency links survive roundtrips. Dependencies refer only to earlier
  options in the same product. Public relations include visible summaries only, without recursion
  or costs. Price ranges describe the positive cheapest effective product price; option filters
  require one matching variant and affect variant-based price ordering.
- MCP `list_products`/`get_product` are client permissions. Costs, markup and hidden products need
  `sales.manage` on the connection itself (owner role intersected with the key's role), and the
  catalog rechecks it against the owner's current role. A staff key narrowed to a client role
  therefore gets the customer shape. Custom roles saved before these permissions existed gain them
  only when re-saved; the built-in roles are updated at startup.

## Decisions

Stock is displayed but never reserved, enforced or decremented, matching the original policy.
Checkout and upsell acceptance are explicitly pending/unpaid; there is no simulated PayFast
charge or public payment-state setter. Finalization closes offers only, and does not send mail or
claim payment. Staff line/order discounts, negotiated shipping without shipping coupon benefit, tax, unique positive
invoice overrides and pending/unpaid editing are permission-protected. Provider payment orchestration
and refunds remain separate. Invoice/quote delivery uses an idempotent provider port; absence or
provider rejection never masquerades as delivery. Adapters distinguish a durable queue from actual
provider acceptance; consumers retry the same request to refresh a queued outcome. HQ uses Email's
outbox and body-free delivery query, without live provider calls in request transactions. A terminal
outbox rejection remains unsuccessful; Sales does not silently create another business event.
Staff quotes use calculated or authorized negotiated prices.
Cart persistence stays browser-owned; neither guest nor signed-in callers require a server cart row.

## Traps and limits

Shipping coupon evaluation uses the pure basket-pricing service so bridges cannot introduce a
Sales -> Shipping -> Sales checkout dependency cycle. An address requires a shipping adapter;
physical companies must opt into requiring addresses. Embedded addresses do not assert ownership
of another account's saved address. HQ's Geo bridge returns area precision, not verified street
or postal accuracy; companies may replace it. Accepted quotes convert only while valid and only once;
conversion does not apply later catalog or shipping changes. Product/variant ids must survive catalog edits.
Queries filter and page before calculating returned prices; pricing loads only the bounded component
graph. Durable definitions/snapshots use independent frozen V1 storage records and version/kind envelopes;
future storage versions need explicit readers/migration.
Wire enums use strict string converters; mapping them must keep frozen V1 persisted enums numeric.
Relational variant selections, daily invalidated
price projections and rating aggregates support database filtering/sorting before paging. Projections
use durable generation stamps and resumable 32-product transactions, without a catalog size ceiling.
Name queries bypass projections; price queries warm one eligible batch and return retryable 503
while their candidates remain stale. The host must run registered workers. Catalog/promotion edits
invalidate all dependent prices; queries never mix stale projections with current calculated prices.
Single shapes for cart, price and receipts are
intentional; staff catalog/order shapes add costs. Aggregate request and graph bounds are policy,
not claims of unlimited cart or bundle support. Company coupon codes and fixed geography belong
in consumer configuration/data, never in this service.
