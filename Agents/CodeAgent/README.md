# Code Agent

Implements code from either an approved GitHub issue plan or a direct local prompt.

## GitHub workflow

Run from the implementation worktree after `PlanReviewAgent` returns `APPROVED`:

```bash
python new CodeAgent "123"
```

If the agent asks questions, reply with the same issue and returned session ID:

```bash
python reply CodeAgent "session_id" "123" "Your answers"
```

Review findings can be sent to that same session with `python reply CodeAgent`. The agent edits only the current worktree; it does not create branches, commit, push, merge, edit the issue, or use local plan files.

## Local prompt

Run from the directory the agent should edit and pass the implementation instructions unchanged:

```bash
python local CodeAgent "Implement the task described in C:\\plans\\task.md"
```

Local mode does not require or access a GitHub issue, approved issue plan, branch, or worktree. The prompt may contain the task directly or reference a task or plan location.

Preset: `gpt-5.6-sol`, medium reasoning, workspace-write sandbox. Requires Python 3.10+ and an authenticated `codex` CLI on `PATH`; the GitHub workflow also requires authenticated `gh`.
