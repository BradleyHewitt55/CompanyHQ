# Admin overrides and custom views

> **Document status:** Confirmed Target V1 contract.

The supplied admin is private fallback code. Installations register sparse components or pages against documented slot IDs and public props/contracts; they do not fork or import private admin paths. Overrides live under the installation `admin/` directory, are version controlled, and compile into a generated registry.

Supported slots include root layout chrome, dashboard panels, navigation groups, collection list/detail headers/actions, field components, cells, auth/account views, workflow/revision panels, and declared custom pages. An override cannot replace authentication, authorization, mutation services, audit, or tenant context.

Missing/invalid overrides fail closed to the supplied component where safe; contract/version mismatches block deployment. Custom pages declare route, policy, data loader command/query, DTO, navigation metadata, error boundary, and tests. Route collisions fail compilation.
