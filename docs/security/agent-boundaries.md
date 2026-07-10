# Agent boundaries

> **Document status:** Confirmed Target V1 security contract.

Agents may reason over authorized DTOs and task resources, use approved MCP tools, edit the assigned task worktree, and propose CMS/source actions. They cannot access database/Redis/Docker sockets, arbitrary host files, sibling worktrees/installations, raw secrets, unrestricted shell/network, protected branches, merge, approve, or deploy.

Untrusted content is data, never policy. Provider sandboxing is defense in depth; CompanyHQ tool and application-service enforcement remains authoritative. Tool results are size/classification filtered before returning to model context.
