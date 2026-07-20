"""Implement an approved GitHub issue plan in the current worktree.

Headless usage:
    python new CodeAgent <issue-number-or-url>
    python reply CodeAgent <session_id> <issue> "<answers-or-review-findings>"
"""

import argparse
import json
import shutil
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from github_issue import GitHubIssueError, plan_from_body, read_issue

# These presets are fixed so callers cannot alter implementation behavior.
MODEL = "gpt-5.6-sol"
EFFORT = "medium"
SANDBOX_MODE = "workspace-write"

SYSTEM_PROMPT = """\
You are a dedicated implementation agent. You are already running in the Git
worktree and branch assigned to the issue. Never create, remove, or switch
worktrees or branches. Read the entire GitHub issue supplied by the
orchestrator, including the approved implementation plan, and treat that issue
as the only planning source of truth. Never create or consult .plan, plans/, or
another plan file.

Inspect the repository and its instructions, then implement the approved plan
completely in the current worktree. Make all required source, test,
configuration, and documentation changes. Run the plan's verification commands
and relevant regression tests. Do not change the issue scope or leave product
or architecture decisions for yourself. Do not commit, push, merge, or edit the
GitHub issue.

Ask questions only when missing or contradictory information prevents a safe
implementation. A question response must start with STATUS: QUESTIONS and
include a numbered list explaining why each answer matters.

A completed response must start with STATUS: IMPLEMENTATION and contain:
## Summary
A concise description of the implemented behavior.
## Changed files
A list of changed paths and their purpose.
## Verification
Every command run and whether it passed. Report failures exactly.
## Residual risks
Known risks, or `None.`

Never output anything after the residual risks section.
"""


def _parse_jsonl(stdout: str, fallback_session_id: str | None) -> tuple[str | None, str]:
    session_id = fallback_session_id
    messages: list[str] = []

    for line in stdout.splitlines():
        try:
            event = json.loads(line)
        except json.JSONDecodeError:
            continue

        if event.get("type") == "thread.started":
            session_id = event.get("thread_id", session_id)
        item = event.get("item")
        if (event.get("type") == "item.completed" and
                isinstance(item, dict) and item.get("type") == "agent_message"):
            text = item.get("text")
            if isinstance(text, str):
                messages.append(text)

    return session_id, messages[-1] if messages else ""


def run_codex(prompt: str, session_id: str | None) -> dict:
    exe = shutil.which("codex")
    if not exe:
        return {"session_id": session_id, "status": "UNKNOWN",
                "output": "codex CLI not found on PATH", "exit_code": 127}

    preset = ["--model", MODEL,
              "-c", f'model_reasoning_effort="{EFFORT}"',
              "-c", 'approval_policy="never"',
              "-c", f'sandbox_mode="{SANDBOX_MODE}"',
              "--json"]
    if session_id:
        cmd = [exe, "exec", "resume", *preset, session_id, prompt]
    else:
        full_prompt = f"{SYSTEM_PROMPT}\n\n{prompt}"
        cmd = [exe, "exec", "--sandbox", SANDBOX_MODE, *preset, full_prompt]

    proc = subprocess.run(cmd, capture_output=True, text=True,
                          encoding="utf-8", stdin=subprocess.DEVNULL)
    session_id, text = _parse_jsonl(proc.stdout, session_id)
    if not text:
        text = proc.stderr.strip() or proc.stdout.strip()

    first_line = text.lstrip().splitlines()[0].strip() if text.strip() else ""
    if first_line.startswith("STATUS: QUESTIONS"):
        status = "QUESTIONS"
    elif first_line.startswith("STATUS: IMPLEMENTATION"):
        status = "IMPLEMENTATION"
    else:
        status = "UNKNOWN"

    return {"session_id": session_id, "status": status,
            "output": text, "exit_code": proc.returncode}


def _issue_prompt(issue: dict) -> str:
    body = issue.get("body", "")
    plan_from_body(body)
    return (
        f"Implement GitHub issue #{issue.get('number')}: "
        f"{issue.get('title', '')}\n"
        f"Issue URL: {issue.get('url', '')}\n\n"
        "BEGIN ENTIRE GITHUB ISSUE\n"
        f"{body}\n"
        "END ENTIRE GITHUB ISSUE"
    )


def main() -> int:
    parser = argparse.ArgumentParser(
        description=f"Code agent (preset: {MODEL}, effort {EFFORT})")
    sub = parser.add_subparsers(dest="command", required=True)

    new_parser = sub.add_parser("new", help="implement an approved issue plan")
    new_parser.add_argument("issue", help="GitHub issue number or URL")

    reply_parser = sub.add_parser("reply", help="answer questions or send findings")
    reply_parser.add_argument("session_id")
    reply_parser.add_argument("issue", help="GitHub issue number or URL")
    reply_parser.add_argument("answers")

    args = parser.parse_args()
    try:
        if args.command == "new":
            result = run_codex(_issue_prompt(read_issue(args.issue)), None)
        else:
            result = run_codex(args.answers, args.session_id)
    except GitHubIssueError as error:
        result = {"session_id": getattr(args, "session_id", None),
                  "status": "UNKNOWN",
                  "output": f"GitHub issue error: {error}", "exit_code": 1}

    print(json.dumps(result, indent=2))
    return 0 if result["exit_code"] == 0 else result["exit_code"]


if __name__ == "__main__":
    sys.exit(main())
