# Code Review Agent

Performs a static, read-only review of the current worktree against the complete GitHub issue and approved plan. It reports defects, potential omissions, code bloat, and maintainability smells without running builds or tests.

## Usage

Run from the implementation worktree after `CodeAgent` completes:

```bash
python new CodeReviewAgent "123"
```

If essential context is missing, reply with the same issue and returned session ID:

```bash
python reply CodeReviewAgent "session_id" "123" "Your answers"
```

Start a fresh review after each fix. The agent never edits files or uses local plan files.

Preset: `claude-opus-4-8`, medium reasoning, plan permission mode. Requires Python 3.10+ and authenticated `claude` and `gh` CLIs on `PATH`.
