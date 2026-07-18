# CodeAgent

Invoke the registered script with the plan name:

```powershell
cms python code-agent.py --plan platform-v3
```

`--plan` is the only supported runtime input. The orchestrator must create matching resources before calling CodeAgent:

| Resource | Required convention for plan `platform-v3` |
|---|---|
| Plan | `Plans/platform-v3.md` |
| Branch | `platform-v3` |
| Worktree | `../CompanyHQ-worktrees/platform-v3` |

CodeAgent resolves and validates all three itself, then runs Codex inside that worktree to implement the plan. It never creates a worktree, creates or switches a branch, commits, or accepts another target.

## Fixed configuration

| Setting | Value |
|---|---|
| Agent | Codex |
| Model | `gpt-5.6-sol` |
| Reasoning | `medium` |
| Permissions | workspace-write |

Codex runs with `--sandbox workspace-write`, so it can only modify files inside the plan's worktree. The prompt carries the project code rules (no `.repos` access, use the `Docs/` references when stuck, no comment-justified workarounds, no scope beyond the plan). Results print to the terminal and are saved in `Agents/CodeAgent/results/`.
