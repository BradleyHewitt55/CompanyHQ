# MCP agent identity

> **Document status:** Target V1 contract.

`AgentIdentity` contains stable agent/profile/connection IDs, provider kind/instance, role, delegating user/service, and policy revision. `RunIdentity` adds task, attempt, session, model, and capability IDs. Provider-supplied labels are metadata, not identity.

Identity is minted by CompanyHQ after provider/profile/task validation and cannot be changed by prompt or tool arguments. All tool results and audits retain both agent and human/service attribution.
