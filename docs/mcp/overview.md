# MCP overview

> **Document status:** Confirmed Target V1 contract; MCP is mandatory.

Each installation exposes one logical MCP boundary. It is an application-service transport for governed CMS, task, project, and agent operations—not a database, shell, filesystem, Redis, Docker, or secret interface. Local and remote transports share the same schemas, authorization, DTO projection, audit, limits, and revocation.

Every call is attributable to installation/company, user/delegator, agent, agent role, connection, profile, task, run, MCP session, tool/resource/prompt, timestamp, correlation, and capability. Authorization runs on discovery and every invocation.
