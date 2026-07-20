# CLI agents

## Decision

`AGENTS.md` is the **orchestrator** for headless CLI agents under `Agents/`. Each implementation has one GitHub issue as its planning source of truth and one title-derived name shared by its plan, branch, and isolated Git worktree under `.worktrees/`.

```text
AGENTS.md orchestrator
  ├─ GitHub CLI ──HTTPS──> issue description
  ├─ PlanAgent ─────────HTTPS──> managed draft plan in issue
  ├─ PlanReviewAgent ───HTTPS──> checks request against draft plan
  ├─ CodeAgent ─────────HTTPS──> reads issue and edits .worktrees/<work-name>
  ├─ CodeReviewAgent ───HTTPS──> statically reviews issue implementation
  └─ ReviewAgent ───────HTTPS──> verifies issue plan and worktree
```

- **Orchestrator**: [`AGENTS.md`][1] creates the issue and worktree, relays questions and findings, and repeats implementation and review until safe to merge.
- **Agent launcher**: [`agent_launcher.py`][2] routes `python new <AGENT>` and `python reply <AGENT>` to a preset agent without changing its arguments or exit code.
- **Plan agent**: [`plan_agent.py`][3] investigates the worktree and writes its completed draft into managed markers in the issue description.
- **Plan review agent**: [`plan_review_agent.py`][4] compares the issue's exact request and decisions with the draft, validates repository claims, and blocks ambiguous plans before implementation.
- **Code agent**: [`code_agent.py`][5] reads the entire issue and implements its approved plan in the current branch and worktree.
- **Code review agent**: [`code_review_agent.py`][6] performs a read-only static review for defects, omissions, bloat, and code smells.
- **Review agent**: [`review_agent.py`][7] reads the approved issue plan, reviews the current worktree, and runs required verification.
- **GitHub issue adapter**: [`github_issue.py`][8] owns issue reads, plan extraction, and idempotent plan replacement.

### Constraints

- **Explicit use only.** The orchestrator starts this workflow only when the user requests agents.
- **Preset values are frozen.** Model, effort, permission mode, and sandbox mode remain constants inside each wrapper.
- **No local plan files.** Agents never create or consult `.plan`, `plans/`, or another plan file.
- **One shared work name.** The lowercase kebab-case issue title is used unchanged for the branch and `.worktrees/<work-name>` directory; the plan keeps the exact issue title as its main heading.
- **One isolated workspace.** All planning, implementation, verification, and review commands run in `.worktrees/<work-name>`.
- **Headless contracts.** Wrappers return `{session_id, status, output, exit_code}` as JSON on stdout. Plan persistence is the only orchestration side effect.
- **Multi-turn sessions.** `QUESTIONS` responses resume through the wrapper's `reply` command with the same session ID and issue reference.
- **Orchestrator-owned technical decisions.** Before involving the user, the orchestrator investigates the issue, repository, current behavior, and established patterns and answers routine agent questions from evidence or the strongest behavior-preserving approach.
- **User decision boundary.** The user is asked only when evidence cannot establish a safe answer or a choice changes existing behavior, architecture, security, data, compatibility, or an approved product decision; the orchestrator provides options, a recommendation, and impact.
- **Finalized pre-implementation gate.** CodeAgent starts only when a fresh PlanReviewAgent returns `APPROVED` with `No findings.` and the reviewed managed plan remains unchanged. Every finding returns to PlanAgent, and every plan or requirement change invalidates the prior approval.
- **No agent-level timeout.** Wrappers do not impose subprocess deadlines.

### Implementation flow

```mermaid
sequenceDiagram
    participant O as AGENTS.md
    participant G as GitHub issue
    participant P as PlanAgent
    participant PR as PlanReviewAgent
    participant C as CodeAgent
    participant CR as CodeReviewAgent
    participant W as Worktree
    participant R as ReviewAgent
    O->>G: Create issue
    O->>W: Create title-named branch and worktree
    O->>P: Plan issue
    P->>G: Update description with draft plan
    O->>PR: Review request and draft plan
    PR->>G: Read exact issue and managed plan
    PR-->>O: Plan findings and verdict
    O->>O: Resolve answer from evidence or ask user only at decision boundary
    O->>P: Return every finding and review each revision
    O->>O: Finalize unchanged plan after APPROVED and No findings
    O->>C: Implement finalized approved issue plan
    C->>G: Read entire issue
    C->>W: Edit and verify in current branch
    O->>CR: Static code review
    CR->>G: Read entire issue
    CR->>W: Inspect code without edits
    CR-->>O: Defects, omissions, bloat, and smells
    O->>C: Return code findings until clean
    O->>R: Verify from worktree
    R->>G: Read issue plan
    R-->>O: Findings, verification, and verdict
    O->>C: Return findings and repeat both reviews
```

1. The [orchestrator][1] creates the GitHub issue, derives one work name from its title, and uses that name for both branch and worktree.
2. The [launcher][2] dispatches `python new PlanAgent` to the [plan agent][3], which stores the draft through the [issue adapter][8].
3. The launcher dispatches `python new PlanReviewAgent` to the [plan review agent][4]. It answers questions itself after investigating evidence and asks the user only when a real decision boundary remains. Every finding returns to planning, and every revised plan receives a fresh review.
4. The exact managed plan is finalized only after a fresh review returns `APPROVED` with `No findings.` Any later plan or requirement change invalidates that approval. Only then does the launcher dispatch `python new CodeAgent` to the [code agent][5] from the issue worktree.
5. The launcher dispatches `python new CodeReviewAgent` to the [code review agent][6]. It statically traces the issue to the implementation and reports defects, omissions, bloat, and smells.
6. After the static review is clean, the launcher dispatches `python new ReviewAgent` to the [review agent][7] for implementation verification.
7. The orchestrator returns findings to the code agent session and starts both reviews again until verification passes.

[1]: ../../AGENTS.md
[2]: ../../Agents/agent_launcher.py
[3]: ../../Agents/PlanAgent/plan_agent.py
[4]: ../../Agents/PlanReviewAgent/plan_review_agent.py
[5]: ../../Agents/CodeAgent/code_agent.py
[6]: ../../Agents/CodeReviewAgent/code_review_agent.py
[7]: ../../Agents/ReviewAgent/review_agent.py
[8]: ../../Agents/github_issue.py
