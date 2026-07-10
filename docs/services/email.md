# Email service

Email is an optional CompanyHQ service, not a permanent dependency on one provider. Resend is the initial provider adapter; a later provider replacement must preserve the service contract, tenant boundaries, policy, templates, audit history, and tests.

## Provider-neutral contract

The email service owns approved transactional and marketing workflows, sender identities, template metadata, consent/subscription policy, delivery events, suppression handling, and provider connection health. The selected provider adapter owns only the provider-specific transport.

Changing provider is a governed Change. It validates credentials and domains, maps templates and webhooks, protects idempotency, preserves audit/history, runs delivery tests, and has a rollback plan. A provider switch never exposes API keys to UI or agents.

## Enablement and access

Email is optional: it is absent from a new company until an owner enables it. The service manifest declares:

- human roles and delegated-agent actions;
- DTOs for templates, delivery status, recipients, and campaign metadata;
- API/MCP version mappings and approval classes;
- webhook and job behavior;
- API, UI where applicable, security, provider, and recovery tests.

An agent can draft within the user's access scope. It cannot send, publish, alter subscriptions, view unrestricted recipients, or change provider configuration unless the relevant user role, task, approval, and service policy allow it.

See [Resend provider adapter](email-resend.md).
