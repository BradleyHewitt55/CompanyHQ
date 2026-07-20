"""Review agent: reviews worktree changes against a GitHub issue plan.

Headless — any orchestrator can call it and gets JSON on stdout:
    python new ReviewAgent <issue-number-or-url>
    python reply ReviewAgent <session_id> <issue> "<answers>"
    python local ReviewAgent "<prompt>"

Output JSON: {"session_id": str, "status": "QUESTIONS"|"REVIEW"|"UNKNOWN",
              "output": str, "exit_code": int}
"""

import argparse
import json
import shutil
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from github_issue import GitHubIssueError, plan_from_body, read_issue

# These values are intentionally unavailable as CLI arguments so orchestrators
# cannot weaken or silently alter the review preset.
MODEL = "gpt-5.6-sol"
EFFORT = "medium"
SANDBOX_MODE = "workspace-write"

SYSTEM_PROMPT = """\
You are a dedicated implementation review agent. The request contains the
approved implementation plan read from the GitHub issue. Treat that issue plan
as the only planning source of truth; never use a .plan, plans/, or other plan
file. Read the entire plan, inspect the implemented code and repository state,
and verify that every requirement, constraint, edge case, and verification
step in the plan was implemented correctly.

First perform a static review. Focus on defects that materially affect
correctness, security, reliability, performance, or maintainability. Call out
missing coverage only when it protects a concrete risk. Do not report
preferences or cosmetic style issues. If you find no static issues, run the
plan's relevant build and test commands to confirm the implementation builds
and passes. You may run commands that generate normal build or test artifacts,
but never edit source, configuration, documentation, or test files. If a
command cannot run, report the exact reason rather than claiming success.

If the plan is ambiguous, incomplete, or does not identify enough scope to
review the implementation, ask questions before reviewing. Otherwise complete
the review without asking unnecessary questions.

Response contract — your final message MUST start with exactly one of:
STATUS: QUESTIONS
  followed by a numbered list of questions and why each answer matters.
STATUS: REVIEW
  followed by:
  ## Findings
  Findings ordered by severity. Each finding must use the form
  - [P0|P1|P2|P3] Short title — path/to/file:line
    A concise explanation of the failure, when it occurs, and its impact.
  If there are no findings, write `No findings.`
  ## Verification
  List each build or test command run and whether it passed. If static findings
  prevented verification, write `Not run because static findings remain.`
  ## Verdict
  One concise sentence stating whether the implementation matches the plan and
  is safe to merge. It is safe only when there are no findings and all required
  build and test commands pass.

Never output anything after the verdict. Never edit files yourself.
"""

LOCAL_SYSTEM_PROMPT = SYSTEM_PROMPT.replace(
    "The request contains the\n"
    "approved implementation plan read from the GitHub issue. Treat that issue plan\n"
    "as the only planning source of truth; never use a .plan, plans/, or other plan\n"
    "file.",
    "The caller's prompt contains the implementation plan or references its\n"
    "location. Treat that supplied plan as the planning source of truth.",
) + """

For this local invocation, review the current working directory and do not
access GitHub. Apply the same verification and response contract.
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


def run_codex(
        prompt: str,
        session_id: str | None,
        system_prompt: str = SYSTEM_PROMPT,
        prompt_heading: str = "Approved implementation plan:\n") -> dict:
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
        full_prompt = f"{system_prompt}\n\n{prompt_heading}{prompt}"
        cmd = [exe, "exec", "--sandbox", SANDBOX_MODE, *preset, full_prompt]

    proc = subprocess.run(cmd, capture_output=True, text=True,
                          encoding="utf-8", stdin=subprocess.DEVNULL)

    session_id, text = _parse_jsonl(proc.stdout, session_id)
    if not text:
        text = proc.stderr.strip() or proc.stdout.strip()

    first_line = text.lstrip().splitlines()[0].strip() if text.strip() else ""
    if first_line.startswith("STATUS: QUESTIONS"):
        status = "QUESTIONS"
    elif first_line.startswith("STATUS: REVIEW"):
        status = "REVIEW"
    else:
        status = "UNKNOWN"

    return {"session_id": session_id, "status": status,
            "output": text, "exit_code": proc.returncode}


def main() -> int:
    parser = argparse.ArgumentParser(description="Review agent (preset: "
                                     f"{MODEL}, effort {EFFORT})")
    sub = parser.add_subparsers(dest="command", required=True)

    p_new = sub.add_parser("new", help="review against a GitHub issue plan")
    p_new.add_argument("issue", help="GitHub issue number or URL")

    p_reply = sub.add_parser("reply", help="answer the agent's questions")
    p_reply.add_argument("session_id", help="session_id from a previous call")
    p_reply.add_argument("issue", help="GitHub issue number or URL")
    p_reply.add_argument("answers", help="answers to the agent's questions")

    p_local = sub.add_parser(
        "local", help="review implementation against a direct prompt")
    p_local.add_argument("prompt")

    args = parser.parse_args()
    try:
        if args.command == "new":
            issue = read_issue(args.issue)
            plan = plan_from_body(issue.get("body", ""))
            prompt = (f"GitHub issue #{issue.get('number')}: "
                      f"{issue.get('title', '')}\n"
                      f"Issue URL: {issue.get('url', '')}\n\n{plan}")
            result = run_codex(prompt, session_id=None)
        elif args.command == "local":
            result = run_codex(
                args.prompt,
                session_id=None,
                system_prompt=LOCAL_SYSTEM_PROMPT,
                prompt_heading="",
            )
        else:
            result = run_codex(args.answers, session_id=args.session_id)
    except GitHubIssueError as error:
        result = {"session_id": getattr(args, "session_id", None),
                  "status": "UNKNOWN",
                  "output": f"GitHub issue error: {error}", "exit_code": 1}

    print(json.dumps(result, indent=2))
    return 0 if result["exit_code"] == 0 else result["exit_code"]


if __name__ == "__main__":
    sys.exit(main())
