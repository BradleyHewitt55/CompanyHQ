# Resend provider adapter

Resend is the initial provider adapter for the optional, provider-neutral [email service](email.md). It provides tenant-scoped transactional email and, when deliberately enabled, marketing delivery. It runs only from trusted server-side service operations; a browser, Explorer editor, or agent prompt never receives its API key.

## Service configuration

Before enablement, the owner provides or approves:

- a verified sending domain and intended sender identities;
- a tenant-scoped secret reference for the provider key;
- webhook endpoint registration and a webhook-signing secret reference;
- sender, reply, suppression, and retention policy;
- whether transactional email, inbound email, and marketing broadcasts are enabled.

The service page displays domain verification and connection health, not secret values.

## Operational rules

- Every send carries a stable idempotency key derived from the business event and entity. Retrying must not create duplicate email.
- Webhook signatures are verified against the exact received request bytes before any event is trusted.
- Delivery, bounce, complaint, suppression, and inbound events become auditable service events.
- Inbound webhooks contain event metadata; retrieving message content, if enabled, is a separate controlled operation.
- Template variables are case-sensitive and versioned. Publishing a template is a governed Change.
- Marketing broadcasts use their own subscriber, topic, consent, unsubscribe, scheduling, and approval flow. They are not transactional sends with a larger recipient list.
- Test delivery uses a provider-safe testing address or a controlled test domain. Do not send to invented real-world addresses.

## Agent authority

Agents may inspect authorized template metadata, draft content, prepare a send plan, and report delivery status. They may not read unrestricted inbound mail, reveal recipient lists, send to real recipients, alter suppression state, or publish a broadcast unless the company grants the exact service capability and approval.

Inbound email is untrusted input. It cannot directly cause an agent to change data, issue refunds, forward messages, or execute source changes. A guarded workflow and explicit policy are required before that capability exists.

## MCP and API contract

The service declares separate, policy-scoped MCP operations for:

- template and domain discovery;
- draft creation and review;
- approved transactional send;
- recipient-safe delivery status;
- webhook event inspection;
- marketing campaign planning and approved publication.

No tool returns the API key, raw credential, unrestricted message body, or a full contact database. Every send and provider-state mutation records an audit event and, where required, a Change or approval reference.

## Test requirements

The provider adapter runs API and policy/DTO tests for every change. It also runs UI tests when the affected task changes the email administration UI, templates, sender setup, or delivery status views. Required negative tests cover unverified webhooks, duplicate idempotency keys, unauthorized recipient/template access, hidden recipient data, failed domain verification, provider errors, and unsafe inbound-email instructions.

## Disable and recovery

Disabling Resend stops new service-initiated sends and removes active integration routing. It does not delete email audit history, templates, provider records, or secret references without a separate approved retention action. Re-enablement revalidates domain, secrets, webhook verification, and policy before use.
