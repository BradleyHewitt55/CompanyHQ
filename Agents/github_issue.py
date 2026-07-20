"""GitHub issue access shared by the headless planning and review agents."""

import json
import shutil
import subprocess

PLAN_START = "<!-- agent-plan:start -->"
PLAN_END = "<!-- agent-plan:end -->"


class GitHubIssueError(RuntimeError):
    """Raised when an issue cannot provide the workflow's source of truth."""


def _gh() -> str:
    exe = shutil.which("gh")
    if not exe:
        raise GitHubIssueError("gh CLI not found on PATH")
    return exe


def read_issue(issue: str) -> dict:
    proc = subprocess.run(
        [_gh(), "issue", "view", issue, "--json", "number,title,body,url"],
        capture_output=True,
        text=True,
        encoding="utf-8",
        stdin=subprocess.DEVNULL,
    )
    if proc.returncode != 0:
        raise GitHubIssueError(proc.stderr.strip() or proc.stdout.strip())

    try:
        payload = json.loads(proc.stdout)
    except (json.JSONDecodeError, TypeError) as error:
        raise GitHubIssueError("gh returned invalid issue JSON") from error

    if not isinstance(payload, dict):
        raise GitHubIssueError("gh returned invalid issue JSON")
    return payload


def plan_from_body(body: str) -> str:
    start = body.find(PLAN_START)
    end = body.find(PLAN_END)
    if start == -1 or end == -1 or end < start:
        raise GitHubIssueError("GitHub issue does not contain an implementation plan")

    plan = body[start + len(PLAN_START):end].strip()
    if plan.startswith("## Implementation plan"):
        plan = plan[len("## Implementation plan"):].strip()
    if not plan:
        raise GitHubIssueError("GitHub issue implementation plan is empty")
    return plan


def update_plan(issue: str, body: str, plan: str, title: str) -> None:
    plan = plan.strip()
    if plan.startswith("STATUS: PLAN"):
        plan = plan[len("STATUS: PLAN"):].strip()

    lines = plan.splitlines()
    if lines and lines[0].startswith("# "):
        plan = "\n".join(lines[1:]).lstrip()
    plan = f"# {title.strip()} plan\n\n{plan}"

    block = f"{PLAN_START}\n{plan}\n{PLAN_END}"
    start = body.find(PLAN_START)
    end = body.find(PLAN_END)

    if start == -1 and end == -1:
        updated_body = f"{body.rstrip()}\n\n{block}\n"
    elif start != -1 and end != -1 and end >= start:
        updated_body = f"{body[:start]}{block}{body[end + len(PLAN_END):]}"
    else:
        raise GitHubIssueError("GitHub issue contains malformed plan markers")

    proc = subprocess.run(
        [_gh(), "issue", "edit", issue, "--body-file", "-"],
        input=updated_body,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    if proc.returncode != 0:
        raise GitHubIssueError(proc.stderr.strip() or proc.stdout.strip())
