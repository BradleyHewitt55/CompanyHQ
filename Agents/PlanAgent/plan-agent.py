#!/usr/bin/env python3
"""Return an implementation recommendation from the preconfigured Codex planner.

Usage:
    cms python plan-agent.py --request "Add tenant audit logging"
"""

from __future__ import annotations

import argparse
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

TIMEOUT_S = 1800

# Planning is read-only. The orchestrator creates plans, branches, and worktrees.
AGENT = "codex"
MODEL = "gpt-5.6-sol"
REASONING = "low"
SANDBOX = "read-only"

BASE_CONTEXT = """You are the CompanyHQ implementation planning advisor. Inspect
the current worktree before recommending an approach. Consult relevant project
documentation and authoritative technology documentation when needed to select the
cleanest approach. Return a precise, implementation-ready Markdown recommendation
only; do not modify files, create a plan file, create a branch, or create a
worktree. Include the goal, relevant existing behavior, the recommended approach,
numbered implementation steps with affected files, data/API/UI changes, validation,
risks, and explicitly identified assumptions or open questions. Keep the scope
limited to the requested work. Do not include an approval preamble."""


def repo_root() -> Path:
    proc = subprocess.run(
        ["git", "rev-parse", "--show-toplevel"], capture_output=True, text=True
    )
    if proc.returncode != 0:
        sys.exit("error: plan-agent must run inside a git worktree")
    return Path(proc.stdout.strip()).resolve()


def build_prompt(request: str) -> str:
    return (
        f"{BASE_CONTEXT}\n\n"
        "## Requested work\n"
        f"{request.strip()}\n\n"
        "## Output\n"
        "The orchestrator will create any plan, branch, and worktree after receiving "
        "your recommendation. Start with a title and return only the recommendation."
    )


def run_planner(prompt: str, cwd: Path) -> tuple[bool, str]:
    binary = shutil.which(AGENT)
    if binary is None:
        return False, f"'{AGENT}' not found on PATH"

    fd, temporary_output = tempfile.mkstemp(prefix="plan_codex_", suffix=".md")
    os.close(fd)
    output_file = Path(temporary_output)
    command = [
        binary,
        "exec",
        "--sandbox",
        SANDBOX,
        "-m",
        MODEL,
        "-c",
        f"model_reasoning_effort={REASONING}",
        "--output-last-message",
        str(output_file),
    ]
    try:
        proc = subprocess.run(
            command,
            input=prompt,
            capture_output=True,
            text=True,
            encoding="utf-8",
            cwd=cwd,
            timeout=TIMEOUT_S,
        )
    except subprocess.TimeoutExpired:
        return False, f"timed out after {TIMEOUT_S}s"

    try:
        if proc.returncode != 0:
            return False, proc.stderr.strip() or proc.stdout.strip() or "(no output)"
        return (
            True,
            output_file.read_text(encoding="utf-8").strip()
            if output_file.exists()
            else proc.stdout.strip(),
        )
    finally:
        output_file.unlink(missing_ok=True)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--request", required=True, help="work to plan")
    args = ap.parse_args()

    root = repo_root()
    print(f"[plan-agent] planning '{args.request}' with {AGENT}/{MODEL} ({REASONING})...")
    ok, output = run_planner(build_prompt(args.request), root)
    if not ok:
        print(f"[plan-agent] FAILED\n{output}", file=sys.stderr)
        return 1

    print("\n===== recommended plan =====\n")
    print(output)
    return 0


if __name__ == "__main__":
    sys.exit(main())
