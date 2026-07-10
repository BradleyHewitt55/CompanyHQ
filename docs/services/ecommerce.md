# Ecommerce service

Ecommerce is an optional business service. It owns commerce-domain records and contributes pages, APIs, MCP tools, pipeline events, and project integrations only after enablement.

## Intended scope

The initial service may manage products, catalogues, prices, inventory, carts, orders, discounts, fulfilment state, customers, and reporting. Payment processing is intentionally provider-neutral until a payment provider is selected.

The service must not turn CompanyHQ into an unreviewed financial automation system. Price, tax, payment, refund, discount, inventory adjustment, and fulfilment changes have explicit policy and audit requirements.

## Enablement requirements

Before enabling ecommerce, the Change must identify:

- selected payment and tax providers, if any;
- currency, market, tax, refund, and fulfilment policy;
- data migration and historical-order retention;
- required customer-facing project;
- staff roles and record-level access;
- checkout, order, product, and webhook API operations with MCP parity;
- backup, reconciliation, and failure-recovery plan.

## Admin and Explorer contributions

The ecommerce service uses the ecommerce key and owns paths below:

~~~text
Admin/Services/ecommerce/
  Pages/products/
  Pages/orders/
  Pages/coupons/
  Settings/general/
  Service/
  Models/
~~~

The exact set of pages is registered in the service manifest. A tenant can override a registered view by exact reference, but cannot replace order authorization, payment verification, or financial audit behavior with presentation code.

## Agent rules

Agents may propose catalogue copy, prepare product edits, identify inventory discrepancies, draft campaigns, and prepare worktree changes within scope. They may not independently charge, refund, alter payout settings, publish price changes, delete orders, or publish inventory-affecting updates without explicit service policy and approval.

## Integration contracts

Ecommerce emits durable business events for catalogue, inventory, order, payment, fulfilment, and refund lifecycle changes. Long-running synchronization, feed generation, and reconciliation run as jobs. Inbound provider webhooks are signature-verified and idempotent before they can update order state.

## Test requirements

Every ecommerce Change runs API, role/record/DTO, webhook/idempotency, migration/reconciliation, and security tests. Run UI tests for any affected admin/client catalogue, order, checkout, or detail page. Financial, price, inventory, refund, and provider changes require negative authorization and approval tests before review.
