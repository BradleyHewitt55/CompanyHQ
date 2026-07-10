# Custom authentication strategies

> **Document status:** Target V1 extension contract.

A custom strategy connects an external identity system or specialized credential to CompanyHQ. It authenticates identity only. Roles, permissions, company membership, DTO fields, service access, and agent authority always come from CompanyHQ policy.

## Strategy contract

Every strategy declares a unique key, owner, enabled companies/projects, accepted transport and credential location, required configuration and secret references, authentication strength, external issuer/audience rules, account-linking policy, error mapping, rate category, audit events, health checks, and security tests.

Its authenticate function receives normalized request metadata and a bounded credential reader. It returns either no identity or a verified identity descriptor containing the strategy key, stable external subject, verified issuer, authentication methods, and safe session hints. It never returns a raw user model, role, permission list, unrestricted headers, or a client-selected company.

## Execution flow

~~~text
select enabled strategy -> read credential from declared location
-> validate format and size -> verify proof with trusted configuration
-> validate issuer/audience/expiry/replay -> resolve an explicit account link
-> check account and company state -> create request principal
-> run local authorization and DTO projection -> audit outcome
~~~

Strategy order is deterministic. A failed credential does not silently fall through to a weaker strategy when that would bypass lockout, MFA, or account policy.

## Account linking

External subjects link to CompanyHQ identities through a unique `(strategy, issuer, subject)` record. Email matching alone never links accounts. First link, relink, issuer change, and recovery require authenticated, audited workflows with collision checks and step-up or owner approval.

## Headers and redirects

Only an approved edge proxy may supply trusted identity headers, and the application accepts them solely from the authenticated proxy boundary after stripping client-supplied copies. Redirect strategies use exact registered redirect URIs, state and nonce validation, proof-key protection where supported, and short-lived single-use transactions.

## Enablement gate

Custom strategies are disabled by default. Enabling or changing one requires a governed Change, threat review, negative authorization tests, account-link collision tests, replay tests, failure and outage behavior, rollback, and documentation. Disabling a strategy blocks new authentication and follows an explicit policy for its existing sessions.
