# Access control

> **Document status:** Confirmed Target V1 security contract.

One default-deny authorization service governs admin UI, API, MCP discovery/execution, application commands, queries, hooks, jobs, agents, source control, and background work. There is no local/internal bypass.

```text
effective grant = human/service principal
  ∩ installation context
  ∩ role grants
  ∩ resource and field attributes
  ∩ task/worktree/tool scope when delegated
  ∩ authentication strength and approval
```

Evaluation returns deny or a constrained grant containing allowed fields, query predicate, relationship depth, actions, and obligations. Query services compile predicates into database queries; callers never fetch broadly and filter in memory. Mutation services enforce field grants against both supplied and derived changes.

Every decision records policy version, actor, resource/action, outcome, and safe reason. Denials must not reveal resource existence or hidden fields. UI navigation is generated from policy but execution always reauthorizes.

See [roles](roles-and-permissions.md), [authorization design](authorization.md), and [agent boundaries](../security/agent-boundaries.md).
