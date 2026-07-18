# PlanAgent

Invoke the registered script with the plan name and the requested work:

```powershell
cms python plan-agent.py --plan tenant-audit-logging --request "Add tenant audit logging"
```

The orchestrator must create matching resources before calling PlanAgent:

| Resource | Required convention for plan `tenant-audit-logging` |
|---|---|
| Branch | `tenant-audit-logging` |
| Worktree | `../CompanyHQ-worktrees/tenant-audit-logging` |

PlanAgent resolves and validates both, then runs Codex read-only inside that worktree to produce an implementation-ready plan. On success the script writes the plan to `Plans/tenant-audit-logging.md` inside the worktree and reports it is ready for CodeAgent. It consults relevant project and authoritative technology documentation when needed.

If the planner cannot decide something from the request or the codebase, it stops with exit code 2 and a `## QUESTIONS FOR ORCHESTRATOR` section. Resume the stored session with:

```powershell
cms python plan-agent.py --plan tenant-audit-logging --answers "1. Log to Postgres."
```

## Fixed configuration

| Setting | Value |
|---|---|
| Agent | Codex |
| Model | `gpt-5.6-terra` |
| Reasoning | `high` |
| Permissions | read-only |

Codex cannot edit the worktree; the wrapper script writes the finished plan file. PlanAgent never creates branches or worktrees; the orchestrator owns those actions. Results print to the terminal and are saved in `Agents/PlanAgent/results/`.
