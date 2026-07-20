"""Plan a GitHub issue and persist the draft plan in that issue.

Headless usage:
    python new PlanAgent <issue-number-or-url>
    python reply PlanAgent <session_id> <issue> "<answers>"
"""

import argparse
import json
import shutil
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from github_issue import GitHubIssueError, read_issue, update_plan

MODEL = "claude-opus-4-8"
EFFORT = "medium"
PERMISSION_MODE = "plan"

SYSTEM_PROMPT = """\
You are a dedicated planning agent. Investigate the repository before planning.
If intent, scope, constraints, or trade-offs are unclear, ask questions rather
than guessing. Never edit repository files. The GitHub issue is the canonical
plan source; never create or reference .plan, plans/, or another plan file.

A question response must start with STATUS: QUESTIONS and include a numbered
list explaining why every answer matters.

A completed response must start with STATUS: PLAN and use this exact structure.
Replace placeholders and repeat T-N and completion-evidence sections as needed:

# <Feature or task> plan

**Status:** Draft
**Revision:** 1
**Owner:** Orchestrator
**Canonical issue:** <GitHub issue URL>

## Original request

<Verbatim or faithful normalized request>

## Goal

<Observable outcome>

## Non-goals

- <Explicit exclusions>

## Acceptance criteria

- **AC-1:** <Testable requirement>
- **AC-2:** <Testable requirement>

## Constraints and security boundaries

- <Compatibility, authorization, data, operational, or scope constraints>

## Verified current state

- `<path-or-symbol>` — <verified fact>

## Assumptions and open decisions

- <Assumption or unresolved decision>

## Approved approach

<Chosen design and why it is the strongest fit>

## Alternatives rejected

- <Alternative> — <reason rejected>

## Implementation tasks

### T-1: <Focused task>

**Objective:** <One observable result>

**Acceptance criteria:** AC-1

**Dependencies:** None

**Files:**
- Create: `<path>`
- Modify: `<path>`
- Test: `<path>`

**Steps:**
1. <Step>
2. <Step>

**Tests:**
- <Test or regression coverage>

**Verification:**
- `<exact command>` — expected: <result>

**Documentation:**
- <Required update or `None: no documented behavior changes`>

**Completion condition:**
- <Observable condition>

**Spec gate:** Pending
**Quality gate:** Pending

## Final integration verification

- `<command>` — expected: <result>

## Rollout, migration, observability, recovery, and rollback

<Required details or `Not applicable` with reason>

## Completion evidence

### T-1

- **Implementation:** Pending
- **Changed files:** Pending
- **Tests:** Pending
- **Verification:** Pending
- **Spec review:** Pending
- **Quality review:** Pending

## Final integration review

- **Verdict:** Pending
- **Residual risks:** Pending

## Revision history

- **Revision 1:** Initial draft.

Do not output anything after the questions or completed plan.
"""


def run_claude(prompt: str, session_id: str | None) -> dict:
    exe = shutil.which("claude")
    if not exe:
        return {"session_id": session_id, "status": "UNKNOWN",
                "output": "claude CLI not found on PATH", "exit_code": 127}

    cmd = [exe, "-p", prompt, "--model", MODEL, "--effort", EFFORT,
           "--permission-mode", PERMISSION_MODE, "--output-format", "json",
           "--append-system-prompt", SYSTEM_PROMPT]
    if session_id:
        cmd += ["--resume", session_id]

    proc = subprocess.run(cmd, capture_output=True, text=True,
                          encoding="utf-8", stdin=subprocess.DEVNULL)
    try:
        payload = json.loads(proc.stdout)
        text = payload.get("result", "")
        session_id = payload.get("session_id", session_id)
    except (json.JSONDecodeError, AttributeError):
        text = proc.stdout or proc.stderr

    first_line = text.lstrip().splitlines()[0].strip() if text.strip() else ""
    status = ("QUESTIONS" if first_line.startswith("STATUS: QUESTIONS") else
              "PLAN" if first_line.startswith("STATUS: PLAN") else "UNKNOWN")
    return {"session_id": session_id, "status": status,
            "output": text, "exit_code": proc.returncode}


def _issue_prompt(issue: dict) -> str:
    return (f"Plan GitHub issue #{issue.get('number')}: {issue.get('title', '')}\n"
            f"Issue URL: {issue.get('url', '')}\n\n"
            f"Issue description:\n{issue.get('body', '')}")


def _error_result(message: str, session_id: str | None = None) -> dict:
    return {"session_id": session_id, "status": "UNKNOWN",
            "output": message, "exit_code": 1}


def main() -> int:
    parser = argparse.ArgumentParser(
        description=f"Planning agent (preset: {MODEL}, effort {EFFORT})")
    sub = parser.add_subparsers(dest="command", required=True)

    new_parser = sub.add_parser("new", help="start planning a GitHub issue")
    new_parser.add_argument("issue", help="GitHub issue number or URL")

    reply_parser = sub.add_parser("reply", help="answer planning questions")
    reply_parser.add_argument("session_id")
    reply_parser.add_argument("issue", help="GitHub issue number or URL")
    reply_parser.add_argument("answers")

    args = parser.parse_args()
    try:
        if args.command == "new":
            issue = read_issue(args.issue)
            result = run_claude(_issue_prompt(issue), None)
        else:
            result = run_claude(args.answers, args.session_id)

        if result["status"] == "PLAN" and result["exit_code"] == 0:
            issue = read_issue(args.issue)
            update_plan(args.issue, issue.get("body", ""), result["output"],
                        issue.get("title", ""))
    except GitHubIssueError as error:
        result = _error_result(f"GitHub issue error: {error}",
                               getattr(args, "session_id", None))

    print(json.dumps(result, indent=2))
    return 0 if result["exit_code"] == 0 else result["exit_code"]


if __name__ == "__main__":
    sys.exit(main())
