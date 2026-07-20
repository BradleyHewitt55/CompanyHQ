# Plan Agent

Creates a draft implementation plan for a GitHub issue without editing repository files. A completed draft is written into the issue description between managed markers and uses the exact issue title as its main heading.

## Usage

Start a plan from an issue number or URL:

```bash
python new PlanAgent "123"
```

If the agent asks questions, reply with the same issue and returned session ID:

```bash
python reply PlanAgent "session_id" "123" "Your answers"
```

Repeat until the returned status is `PLAN`, then run `python new PlanReviewAgent "<issue>"`. The issue description is the plan source of truth; do not create a `.plan` or plan file. Implementation cannot start until plan review returns `APPROVED`.

Requires Python 3.10+, authenticated `claude` and `gh` CLIs on `PATH`, and a current directory belonging to the issue repository when using an issue number.
