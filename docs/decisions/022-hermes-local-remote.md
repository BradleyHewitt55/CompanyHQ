# ADR-022: Hermes local and remote connections

> **Status:** Accepted. **Review:** after the first Hermes protocol spike or by 2027-01-15.

## Context and decision
Hermes may run beside the installation or remotely. Support both through authenticated adapter transports with identical task, MCP, capability, audit, cancellation, and reconnection contracts.

## Alternatives considered
Embed Hermes; local-only; remote-only; expose Hermes tools directly.

## Consequences, risks, security, and migration
Deployment choice improves, but remote compromise/latency and local host access differ. Local is not trusted by location; remote requires TLS and narrow egress. Hermes state never becomes CompanyHQ authority. Protocol incompatibility leaves the adapter disabled.
