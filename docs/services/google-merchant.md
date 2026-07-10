# Google Merchant service

> **Document status:** Target V1 optional service contract.

Google Merchant is an optional distribution service for an enabled ecommerce catalogue and a public customer-facing project. It produces governed product feed and account synchronization work; it does not own products or payment state.

## Dependencies

The service requires:

- the ecommerce service enabled and healthy;
- a public project with an approved product presentation;
- verified account/credential references and market configuration;
- policy for product eligibility, price, availability, shipping, tax, and publication;
- a feed validation, review, retry, and rollback plan.

The service remains blocked rather than partially enabled when a dependency is absent or unhealthy.

## Feed lifecycle

~~~text
catalogue change -> feed draft -> validation -> owner/service approval
-> publish or submit -> provider status -> audit and remediation
~~~

Feed generation is an asynchronous job. It uses durable source data and records the product revision, generated feed revision, validation outcome, publication decision, provider response, and remediation task.

## Agent authority

Agents may inspect product eligibility, prepare feed fixes, draft descriptions, and report disapprovals within the permissions granted by ecommerce and Merchant policy. They may not publish or submit a feed, change account configuration, alter prices, or hide provider errors without explicit approval.

## MCP and API contract

The service registers read-only discovery for feed health and product eligibility, plus governed draft, validation, remediation, and approved publication operations. API/MCP parity, scoped responses, audit events, and Change links are required for every operation.

## Test requirements

Every Merchant Change runs dependency, API/MCP, policy/DTO, feed validation, job/idempotency, provider-error, and rollback tests. Run UI tests for any Merchant dashboard, eligibility, validation, or review-page change.
