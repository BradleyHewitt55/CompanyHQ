# Review Agent

Reviews worktree changes against the implementation plan stored in a GitHub issue, then builds and tests when the static review has no findings.

## Usage

Run from the implementation worktree and pass the issue number or URL:

```bash
python new ReviewAgent "123"
```

If the agent asks questions, reply with the same issue and returned session ID:

```bash
python reply ReviewAgent "session_id" "123" "Your answers"
```

Repeat until the returned status is `REVIEW`. In the GitHub workflow, the agent reads only the managed implementation-plan section in the issue description.

For verification without GitHub, run from the implementation directory and pass the plan or its location:

```bash
python local ReviewAgent "Verify the implementation against C:\\plans\\task.md"
```

Preset: `gpt-5.6-sol`, medium reasoning, workspace-write sandbox. Workspace write access is required for build and test artifacts; the agent is prohibited from editing project files. Requires Python 3.10+ and authenticated `codex`; the GitHub workflow also requires authenticated `gh`.
