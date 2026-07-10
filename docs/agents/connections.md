# Agent connections

> **Document status:** Target V1 architecture contract.

Provider connections are disabled by default and installed per CompanyHQ installation. An owner adds provider configuration and credential references, runs a health/capability probe, creates profiles, reviews tools/network/data scopes, and explicitly enables it.

Provider credentials stay in the installation secret store and are injected only into the adapter process. Status distinguishes configured, healthy, degraded, rate-limited, reauth-required, disabled, and unavailable. Disabling revokes capabilities and prevents new runs before stopping or reconciling active sessions.
