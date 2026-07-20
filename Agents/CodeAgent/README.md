# Code Agent

Implements the approved plan from a complete GitHub issue in the current branch and worktree.

## Usage

Run from the implementation worktree after `PlanReviewAgent` returns `APPROVED`:

```bash
python new CodeAgent "123"
```

If the agent asks questions, reply with the same issue and returned session ID:

```bash
python reply CodeAgent "session_id" "123" "Your answers"
```

Review findings can be sent to that same session with `python reply CodeAgent`. The agent edits only the current worktree; it does not create branches, commit, push, merge, edit the issue, or use local plan files.

Preset: `gpt-5.6-sol`, medium reasoning, workspace-write sandbox. Requires Python 3.10+ and authenticated `codex` and `gh` CLIs on `PATH`.
