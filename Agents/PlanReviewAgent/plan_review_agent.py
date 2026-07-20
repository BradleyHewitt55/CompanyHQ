"""Review a GitHub issue plan before implementation begins.

Headless usage:
    python new PlanReviewAgent <issue-number-or-url>
    python reply PlanReviewAgent <session_id> <issue> "<answers>"
    python local PlanReviewAgent "<prompt>"
"""

import argparse
import json
import shutil
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from github_issue import (GitHubIssueError, PLAN_END, PLAN_START,
                          plan_from_body, read_issue)

# These presets are fixed so callers cannot weaken the review.
MODEL = "claude-opus-4-8"
EFFORT = "medium"
PERMISSION_MODE = "plan"

SYSTEM_PROMPT = """\
You are a dedicated pre-implementation plan review agent. No code agent has
started work. Review the proposed plan against the exact request, decisions,
constraints, and acceptance criteria in the GitHub issue. The issue text is the
requirements source of truth; the proposed plan is not.

Investigate the repository in read-only mode to validate every claim about the
current state, named file, symbol, dependency, command, and test surface. Never
edit files. Never create or consult .plan, plans/, or another plan file.

Find every material plan defect, including:
- a request, decision, constraint, edge case, or acceptance criterion that is
  omitted, weakened, contradicted, or expanded beyond the issue;
- behavior, architecture, ownership, data handling, authorization, failure
  handling, compatibility, migration, rollout, rollback, observability, or
  documentation choices left for the implementer to guess;
- vague steps, hidden dependencies, invalid sequencing, unsupported repository
  claims, wrong paths or symbols, and scope that cannot produce the goal;
- acceptance criteria, tests, or verification commands that are subjective,
  incomplete, non-executable, or fail to prove the stated outcome;
- unnecessary complexity, duplicated responsibility, leaky abstractions, or
  coupling that conflicts with repository architecture.

Trace every requirement and explicit decision to concrete plan tasks, tests,
and verification. Do not invent requirements or report cosmetic preferences.
Do not demand irrelevant mechanical detail, but do flag any unresolved choice
that could change observable behavior, architecture, security, data, failure
semantics, compatibility, or verification. A code agent must be able to execute
an approved plan without interpreting intent or making product/design choices.

If essential issue context is unavailable, respond with STATUS: QUESTIONS and
a numbered list stating exactly what is missing and why it prevents review.
Otherwise the final response must start with STATUS: PLAN_REVIEW and use exactly:

## Findings
Findings ordered by severity. Each finding must use:
- [P0|P1|P2] Short title — <issue requirement or plan section>
  State the mismatch or ambiguity, its implementation impact, and the exact
  decision or plan correction required.
If there are no findings, write `No findings.`

## Repository validation
List the repository facts and paths checked. Do not claim a check you did not
perform.

## Verdict
Write exactly one of:
APPROVED — The plan faithfully and unambiguously implements the issue.
CHANGES REQUIRED — The plan is not ready for implementation.

Never output anything after the verdict. Approval is allowed only when there
are no findings.
"""

LOCAL_SYSTEM_PROMPT = SYSTEM_PROMPT.replace(
    "Never\nedit files. Never create or consult .plan, plans/, or another plan file.",
    "Never edit files. Consult task or plan files only when the caller references\n"
    "them.",
) + """

For this local invocation, the caller's prompt replaces the GitHub issue as the
review source. The prompt may contain the request and proposed plan directly or
reference their locations. Do not access GitHub; apply the same review standard
and response contract.
"""


def _request_from_body(body: str) -> str:
    """Remove only the managed plan block so issue wording stays verbatim."""
    start = body.find(PLAN_START)
    end = body.find(PLAN_END)
    if start == -1 or end == -1 or end < start:
        raise GitHubIssueError("GitHub issue does not contain an implementation plan")
    return f"{body[:start]}{body[end + len(PLAN_END):]}"


def _issue_prompt(issue: dict) -> str:
    body = issue.get("body", "")
    request = _request_from_body(body)
    plan = plan_from_body(body)
    return (
        f"GitHub issue #{issue.get('number')}: {issue.get('title', '')}\n"
        f"Issue URL: {issue.get('url', '')}\n\n"
        "BEGIN EXACT ISSUE REQUEST AND DECISIONS\n"
        f"{request}\n"
        "END EXACT ISSUE REQUEST AND DECISIONS\n\n"
        "BEGIN PROPOSED IMPLEMENTATION PLAN\n"
        f"{plan}\n"
        "END PROPOSED IMPLEMENTATION PLAN"
    )


def run_claude(
        prompt: str,
        session_id: str | None,
        system_prompt: str = SYSTEM_PROMPT) -> dict:
    exe = shutil.which("claude")
    if not exe:
        return {"session_id": session_id, "status": "UNKNOWN",
                "output": "claude CLI not found on PATH", "exit_code": 127}

    cmd = [exe, "-p", prompt, "--model", MODEL, "--effort", EFFORT,
           "--permission-mode", PERMISSION_MODE, "--output-format", "json",
           "--append-system-prompt", system_prompt]
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
              "PLAN_REVIEW" if first_line.startswith("STATUS: PLAN_REVIEW") else
              "UNKNOWN")
    return {"session_id": session_id, "status": status,
            "output": text, "exit_code": proc.returncode}


def main() -> int:
    parser = argparse.ArgumentParser(
        description=f"Plan review agent (preset: {MODEL}, effort {EFFORT})")
    sub = parser.add_subparsers(dest="command", required=True)

    new_parser = sub.add_parser("new", help="review a GitHub issue plan")
    new_parser.add_argument("issue", help="GitHub issue number or URL")

    reply_parser = sub.add_parser("reply", help="provide missing review context")
    reply_parser.add_argument("session_id")
    reply_parser.add_argument("issue", help="GitHub issue number or URL")
    reply_parser.add_argument("answers")

    local_parser = sub.add_parser(
        "local", help="review a plan from a direct prompt")
    local_parser.add_argument("prompt")

    args = parser.parse_args()
    try:
        if args.command == "new":
            result = run_claude(_issue_prompt(read_issue(args.issue)), None)
        elif args.command == "local":
            result = run_claude(args.prompt, None, LOCAL_SYSTEM_PROMPT)
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
