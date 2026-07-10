# Authentication email

> **Document status:** Target V1 security contract.

The authentication service uses the configured [email service](../services/email.md) for address verification, password recovery, security alerts, invitations, and account-change notices. Delivery failure never weakens the underlying authentication decision.

## Verification

Creating an account or changing its sign-in address creates a random, single-use, short-lived verification value. Only its one-way verifier and purpose metadata are stored. The message links to an approved CompanyHQ or project origin; arbitrary request hosts and client-supplied redirect URLs are rejected.

Completing verification validates the value, purpose, account, intended address, company/project, expiry, and unused state, then consumes it atomically. A verification value cannot reset a password or authenticate another account.

## Password recovery

Starting recovery always returns the same public response. When an eligible account exists, a durable job sends a short-lived, single-use recovery link. The link opens a guarded reset flow; the new password and recovery value are submitted directly to the reset operation.

Successful recovery consumes the value, updates the password verifier, increments token version, revokes active sessions according to policy, and sends a security notice. The notice includes a safe recovery route if the change was unexpected, never a credential.

## Message contract

Templates receive only the minimum notification DTO: company/project display name, safe recipient display value, purpose, expiry guidance, approved action URL, and support route. They never receive password hashes, MFA secrets, session values, API keys, raw user models, hidden fields, or unrelated customer data.

URLs use an allowlisted HTTPS origin outside explicit loopback development. Sensitive values are placed in the fragment or a short-lived exchange flow where feasible to reduce referrer exposure, are redacted from access logs, and are never sent to analytics.

## Abuse and delivery controls

Verification and recovery starts are limited by account, source, company/project, and time window. Duplicate requests invalidate or supersede older values according to policy. Delivery jobs are idempotent, retry safely, and expose only a generic delivery state to unauthorized callers.

Template, sender-domain, expiry, redirect-origin, and provider changes are governed, previewed, tested, audited, and reversible.
