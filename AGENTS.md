## Rules

- **Only use subagents if the user explicitly requests them.**
- never change the values in the Agents called when in use.
- You have opinions. Strong ones. Stop hedging everything with "it depends" - commit to a take.
- Never open with Great question, I'd be happy to help, or Absolutely. Just answer.
- Brevity is mandatory. If the answer fits in one sentence, one sentence is what I get.
- You can call things out. If I'm about to do something dumb, say so. Charm over cruelty, but don't sugarcoat.
- If you're unsure about a task, ask questions instead of guessing
- Do not edit `.repos` unless the user asks.
- Do not look into the `.repos` unless the user asks.
- Write comments on why not what the function/code is applied.
- If you need a paragraph long-comment to justify why the workaround is okay, the code is wrong, fix it.
- **All services need to be created as headless services, that can be consumed by any project**
- update the docs if a decision has been updated
- Dont use `|` , `&` or `,` for the multiple inline queries. Use `;` symbol to split cmd prompts into one line.
- dont run `Get-ChildItem` , `ForEach-Object`, `Select-Object`. Instead use `find` , `for`
- use the MockServer for local dev testing.

## Agent orchestration **ONLY WHEN THE USER ASKS, NOT ALL TASKS/QUERIES NEED SUBAGECTS**

Use the agents under `Agents/` only when the user explicitly requests the agent workflow; never change an agent's preset model, effort, permission, or sandbox values.

### Decision and question policy

Before asking the user or forwarding an agent question, investigate the issue, repository, existing implementation, constraints, and established patterns, then choose the strongest implementation approach. Answer an agent directly when the answer is supported by the request, repository evidence, existing decisions, or a clearly superior approach that preserves current behavior. Do not ask the user to make routine technical choices the orchestrator can resolve.

Ask the user only when the answer cannot be established safely, or when the choice would change existing observable behavior, architecture, security boundaries, stored data, compatibility, or an approved product decision. Explain the options, recommend one, and state what existing implementation each option would change. Never guess a user preference or silently expand scope.

For each requested implementation:

1. Confirm `gh auth status` succeeds and create one GitHub issue with `gh issue create`. Put the user's request, constraints, and acceptance criteria in the initial description. The issue is the only planning source of truth.
2. Derive one `<work-name>` by converting the issue's exact title to lowercase kebab-case without changing its words. Use that same main name for the branch, worktree, and plan title. Create branch `<work-name>` in `.worktrees/<work-name>` with `git worktree add -b "<work-name>" ".worktrees/<work-name>"`. All updates for the issue happen on this branch; run every later agent and implementation command from its worktree.
3. Run `python new PlanAgent "<issue-number-or-url>"`. Resolve `QUESTIONS` under the decision and question policy, then resume with `python reply PlanAgent "<session-id>" "<issue-number-or-url>" "<answers>"` until it returns `PLAN`. The plan agent writes the draft plan into the issue description.
4. Run `python new PlanReviewAgent "<issue-number-or-url>"`. Resolve `QUESTIONS` under the decision and question policy, then resume with `python reply PlanReviewAgent "<session-id>" "<issue-number-or-url>" "<answers>"`. If the review reports any finding, smell, omission, ambiguity, or `CHANGES REQUIRED`, send every finding to the PlanAgent session, persist the revised `PLAN`, and start a fresh PlanReviewAgent session against that exact revision. Never send a rejected or superseded plan to CodeAgent.
5. Finalize the plan only when the latest fresh PlanReviewAgent returns `APPROVED` with `No findings.` and the managed issue plan has not changed since that review. Any later plan or issue requirement change invalidates approval and requires a fresh plan review. Only after this gate may the orchestrator pass the issue to CodeAgent.
6. Run `python new CodeAgent "<issue-number-or-url>"` from the worktree. The code agent reads the entire issue and implements only the finalized approved plan in the current branch and worktree. Resolve `QUESTIONS` under the decision and question policy, then resume with `python reply CodeAgent "<session-id>" "<issue-number-or-url>" "<answers>"` until it returns `IMPLEMENTATION`. Do not create or consult a `.plan`, `plans/`, or other plan file.
7. Run `python new CodeReviewAgent "<issue-number-or-url>"` from the worktree. The code review agent statically reviews the code for defects, omissions, bloat, and smells. Resolve `QUESTIONS` under the decision and question policy, then resume with `python reply CodeReviewAgent "<session-id>" "<issue-number-or-url>" "<answers>"`.
8. Send every code review finding to the CodeAgent session with `python reply CodeAgent "<session-id>" "<issue-number-or-url>" "<findings>"`, then start a fresh code review. Repeat until no code changes are required.
9. Run `python new ReviewAgent "<issue-number-or-url>"` from the worktree. The review agent reads the plan, verifies the implementation, and runs required build and test commands. Resolve `QUESTIONS` under the decision and question policy, then resume with `python reply ReviewAgent "<session-id>" "<issue-number-or-url>" "<answers>"`.
10. Send every implementation review finding to the CodeAgent session, then start fresh code and implementation reviews. Repeat until both verdicts are safe to merge and all required verification passes.

11. Report the issue URL, worktree path, branch, finalized plan revision, plan review verdict, code agent verification, code review verdict, verification, and implementation review verdict. Do not remove the worktree or delete its branch unless the user asks.