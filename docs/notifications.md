# Notifications

> **Document status:** Target V1 product contract.

CompanyHQ can notify a user in the admin workspace, desktop operating system, or mobile device. Delivery is tied to the receiving user's company, role, DTO visibility, and notification preferences.

## User controls

Each user manages preferences in:

~~~text
Settings -> Notifications
~~~

They can enable or mute non-critical categories per desktop and mobile device, including task assignment, agent progress, review requests, pull-request status, deployment status, service health, marketing status, and mentions.

Security, account-recovery, access-revocation, and owner-required approval notices remain visible in the admin workspace. Letting a user silently disable those would be a bad security design.

## Safety rules

- A notification contains only a safe notification DTO: title, event category, authorized summary, and a guarded deep link.
- Recheck access when the user opens a notification. A stale notification never grants access to a removed task, company, record, or source item.
- Do not put secrets, raw models, hidden fields, token data, customer PII beyond the authorized DTO, or agent prompt content in a lock-screen or push message.
- Register and revoke devices through authenticated user settings. A shared device must not receive another staff member's alerts.
- An agent may request a notification through a governed event; it cannot address arbitrary devices or bypass user preferences.

## Important events

Notifications may be generated for:

- task assigned to self, delegated to an agent, moved to review, completed, blocked, or mentioned;
- agent run started, needs review, failed, stopped, or revoked;
- pull request created, reviewed, approved, merged, or rejected;
- deployment preview ready, production promotion awaiting approval, rollback, or health failure;
- service enablement, provider health, secret rotation, migration, backup, or security event;
- optional provider task status synchronized into the Pipeline.

Notification delivery is an asynchronous, idempotent job. Its audit record links to the source task, Change, service, or security event.
