# Hermes Agent pattern study

> **Document status:** Reference finding from `.repos/hermes-agent`; not CompanyHQ implementation. Inspected during repository discovery.

Hermes Agent is a Python-oriented personal agent runtime with provider abstraction, tool registration, context assembly, memory, skills, delegated tasks, scheduled execution, and messaging integrations. Its useful patterns are explicit tool schemas, provider-neutral model calls, resumable sessions, context compression, skill discovery, memory retrieval, and user-visible long-running work.

Its state and scheduling are not a tenant-ready CMS foundation: profile-scoped SQLite state, JSON cron configuration, in-process delegation, broad local tools, and personal credential assumptions do not provide independent-company isolation, durable queue leases, central CMS authorization, or approval-safe mutations.

CompanyHQ therefore integrates Hermes only through a provider adapter and the installation-local MCP server. Hermes may reason, plan, and call granted tools; it cannot access PostgreSQL, Redis, host Docker, unrestricted shell/filesystem/network, or secrets. See [Hermes adapter](../agents/hermes.md).
