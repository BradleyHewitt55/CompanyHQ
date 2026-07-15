# ReviewAgent

Invoke the registered script with the plan name:

```powershell
cms python review-agent.py --plan platform-v3
```

`--plan` is the only supported runtime input. The orchestrator must create matching resources before calling ReviewAgent:

| Resource | Required convention for plan `platform-v3` |
|---|---|
| Plan | `Plans/platform-v3.md` |
| Branch | `platform-v3` |
| Worktree | `../CompanyHQ-worktrees/platform-v3` |

ReviewAgent resolves and validates all three itself, then runs Codex inside that worktree. It never creates a worktree, creates or switches a branch, or accepts another target.

## Fixed configuration

| Setting | Value |
|---|---|
| Agent | Codex |
| Model | `gpt-5.6-codex` |
| Reasoning | `medium` |
| Permissions | read-only |

Read-only is enforced twice: Codex runs with `--sandbox read-only`, and the review prompt forbids edits. Results print to the terminal and are saved in `Agents/ReviewAgent/results/`.
