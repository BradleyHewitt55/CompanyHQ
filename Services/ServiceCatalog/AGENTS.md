# Service catalog foundation

## Ownership

Owns immutable public service metadata, allowlist validation, committed-state availability and
operation admission. It owns no credentials, persistence provider, role store or company pages.
Company supplies the always-enabled selection reader; hosts supply loaded provider readiness.

## Rules

- Foundation Contracts stay free of framework, provider and persistence dependencies.
- Runtime reads open a fresh scope so tracked or uncommitted caller selections never grant access.
  Required dependency graphs are acyclic and every required target must be allowlisted.
- Metadata never includes values. Readiness callbacks must avoid admitted interfaces; an operation's
  admission evaluating its own interface would recurse. Capability checks must remain offline.
- Admission wrapping is opt-in and preserves each registration's lifetime and constructor validation.
  Private keyed targets must not shadow host-owned concrete registrations. Concrete operation gates
  also cover services consumers already resolve directly.

## Decisions

Enablement and selected providers are company state; loaded providers/options are process state.
Navigation metadata describes destinations but authorization and safe projection belong to Company.
The catalog does not discover allowlisted features from every referenced assembly automatically.

## Traps and limits

Routing must identify an endpoint before catalog middleware runs, and admission precedes authentication.
Non-MVC endpoints need explicit ownership metadata. Providers and signatures already accepted by an
external system survive a disable; switches apply to subsequent admission, not rollback.
Old standalone registrations retain their behavior until a host explicitly opts into the catalog.
