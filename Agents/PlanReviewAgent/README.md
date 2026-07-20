# Plan Review Agent

Reviews a proposed GitHub issue plan before implementation. It compares the managed plan with the issue's exact request and decisions, validates repository claims, and reports omissions, ambiguity, guesswork, smells, and unverifiable outcomes.

## Usage

Run after `PlanAgent` completes and before implementation starts:

```bash
python new PlanReviewAgent "123"
```

If essential context is unavailable, reply with the returned session ID:

```bash
python reply PlanReviewAgent "session_id" "123" "Your answers"
```

Do not begin implementation until the verdict is `APPROVED`. A `CHANGES REQUIRED` verdict must return to planning and receive a fresh plan review.

Preset: `claude-opus-4-8`, medium reasoning, plan permission mode. Requires Python 3.10+ and authenticated `claude` and `gh` CLIs on `PATH`.
