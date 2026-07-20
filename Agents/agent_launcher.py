"""Dispatch short commands to the preset plan and review agents."""

import subprocess
import sys
from pathlib import Path

_AGENT_SCRIPTS = {
    "PlanAgent": Path("PlanAgent") / "plan_agent.py",
    "PlanReviewAgent": Path("PlanReviewAgent") / "plan_review_agent.py",
    "CodeAgent": Path("CodeAgent") / "code_agent.py",
    "CodeReviewAgent": Path("CodeReviewAgent") / "code_review_agent.py",
    "ReviewAgent": Path("ReviewAgent") / "review_agent.py",
}


def main(command: str, argv: list[str] | None = None) -> int:
    args = list(sys.argv[1:] if argv is None else argv)
    if not args:
        names = ", ".join(_AGENT_SCRIPTS)
        print(f"Usage: python {command} <AGENT> <arguments>\nAgents: {names}",
              file=sys.stderr)
        return 2

    agent = args.pop(0)
    relative_script = _AGENT_SCRIPTS.get(agent)
    if relative_script is None:
        names = ", ".join(_AGENT_SCRIPTS)
        print(f"Unknown agent: {agent}. Expected one of: {names}",
              file=sys.stderr)
        return 2

    script = Path(__file__).resolve().parent / relative_script
    return subprocess.run(
        [sys.executable, str(script), command, *args],
        check=False,
    ).returncode
