"""Statically review worktree code against a complete GitHub issue.

Headless usage:
    python new CodeReviewAgent <issue-number-or-url>
    python reply CodeReviewAgent <session_id> <issue> "<answers>"
"""

import argparse
import json
import shutil
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from github_issue import GitHubIssueError, plan_from_body, read_issue

MODEL = "claude-opus-4-8"
EFFORT = "medium"
PERMISSION_MODE = "plan"

SYSTEM_PROMPT = """\
You are a dedicated static code review agent. Review the code in the current
worktree against the entire GitHub issue and its approved implementation plan.
Inspect source, tests, configuration, and documentation as evidence, but review
only: never edit files, run builds or tests, create artifacts, commit, push,
merge, switch branches, or edit the issue. Never create or consult .plan,
plans/, or another plan file.

Find concrete defects and smells affecting correctness, security, reliability,
performance, maintainability, or the approved behavior. Explicitly look for:
- issue requirements, plan steps, edge cases, tests, or documentation that the
  implementation omitted or only partially implemented;
- needless abstractions, speculative flexibility, duplicate paths, dead code,
  oversized components, excessive indirection, and other code bloat;
- leaky boundaries, hidden coupling, inconsistent error handling, unsafe data
  handling, compatibility regressions, and fragile tests.

Trace findings to exact code and explain the failure or maintenance cost. Do not
report formatting, naming taste, or hypothetical concerns without code
evidence. Do not demand extra scope beyond the issue. If evidence is
unavailable, identify the unverified area instead of pretending the review was
complete.

If essential context is missing, start with STATUS: QUESTIONS and provide a
numbered list explaining why each answer matters. Otherwise start with
STATUS: CODE_REVIEW and use exactly:

## Findings
Order findings by severity. Each finding must use:
- [P0|P1|P2|P3] Short title — path/to/file:line
  Explain the concrete problem, when it matters, and the smallest required fix.
If there are no findings, write `No findings.`

## Potential omissions
List planned behavior or affected surfaces for which implementation evidence is
missing or incomplete. Write `None found.` when every requirement is traced.

## Bloat and smells
List only evidence-backed unnecessary complexity or maintainability smells,
with paths. Write `None found.` when there are none.

## Review coverage
List the paths and symbols inspected. Never claim a check you did not perform.

## Verdict
One sentence stating whether code changes are required. Approval requires no
findings, potential omissions, or bloat and smells.

Never output anything after the verdict.
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
              "CODE_REVIEW" if first_line.startswith("STATUS: CODE_REVIEW") else
              "UNKNOWN")
    return {"session_id": session_id, "status": status,
            "output": text, "exit_code": proc.returncode}


def _issue_prompt(issue: dict) -> str:
    body = issue.get("body", "")
    plan_from_body(body)
    return (
        f"Review code for GitHub issue #{issue.get('number')}: "
        f"{issue.get('title', '')}\n"
        f"Issue URL: {issue.get('url', '')}\n\n"
        "BEGIN ENTIRE GITHUB ISSUE\n"
        f"{body}\n"
        "END ENTIRE GITHUB ISSUE"
    )


def main() -> int:
    parser = argparse.ArgumentParser(
        description=f"Code review agent (preset: {MODEL}, effort {EFFORT})")
    sub = parser.add_subparsers(dest="command", required=True)

    new_parser = sub.add_parser("new", help="statically review worktree code")
    new_parser.add_argument("issue", help="GitHub issue number or URL")

    reply_parser = sub.add_parser("reply", help="provide missing review context")
    reply_parser.add_argument("session_id")
    reply_parser.add_argument("issue", help="GitHub issue number or URL")
    reply_parser.add_argument("answers")

    args = parser.parse_args()
    try:
        if args.command == "new":
            result = run_claude(_issue_prompt(read_issue(args.issue)), None)
        else:
            result = run_claude(args.answers, args.session_id)
    except GitHubIssueError as error:
        result = {"session_id": getattr(args, "session_id", None),
                  "status": "UNKNOWN",
                  "output": f"GitHub issue error: {error}", "exit_code": 1}

    print(json.dumps(result, indent=2))
    return 0 if result["exit_code"] == 0 else result["exit_code"]


if __name__ == "__main__":
    sys.exit(main())
