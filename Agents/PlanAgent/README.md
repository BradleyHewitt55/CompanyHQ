# PlanAgent

Return an implementation recommendation for the orchestrator from the current worktree:

```powershell
cms python plan-agent.py --request "Add tenant audit logging"
```

The agent inspects the repository read-only and prints an implementation-ready Markdown recommendation for the orchestrator. It consults relevant project and authoritative technology documentation when needed.

## Fixed configuration

| Setting | Value |
|---|---|
| Agent | Codex |
| Model | `gpt-5.6-sol` |
| Reasoning | `low` |
| Permissions | read-only |

Codex cannot edit the worktree. PlanAgent never creates plan files, branches, or worktrees; the orchestrator owns those actions.
