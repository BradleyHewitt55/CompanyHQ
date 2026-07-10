# Glossary

> **Document status:** Target V1 terminology contract.

| Term | Definition |
| --- | --- |
| Installation | One company's independent CompanyHQ deployment and isolation boundary. |
| Company context | Validated identity of the current installation, propagated to requests, jobs, files, runs, events, and audit records. |
| Collection | Configured content type with fields, access, admin views, hooks, workflow, and API behavior. |
| Global | Singleton configured document within an installation. |
| Command | Typed request to perform a state change through an application service. |
| Revision | Immutable snapshot of document state and mutation metadata. |
| Task | Durable unit of human or agent work. |
| Run | One execution attempt for a task. |
| Worktree | Git worktree owned by one parent task; subtasks share it. |
| Agent profile | Provider-neutral role, capabilities, policies, budgets, and tool grants. |
| Capability | Short-lived, narrowly scoped authority for one actor and context. |
| Approval | Human decision bound to an exact action, resource version, and expiry. |
| MCP server | Installation-local Model Context Protocol gateway exposing governed CMS tools. |
| Projection | Read model built from authoritative state or events. |
| Outbox | Transactional records used to publish durable events/jobs after commit. |
