#!/usr/bin/env python3
"""Run the preconfigured Codex reviewer for an orchestrator-created plan.

Usage:
    cms python review-agent.py --plan platform-v3
"""

from __future__ import annotations

import argparse
import datetime as dt
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
RESULTS_DIR = SCRIPT_DIR / "results"
TIMEOUT_S = 1800

# This is intentionally not a CLI option. A review agent must not be allowed
# to edit the worktree it is assessing.
AGENT = "codex"
MODEL = "gpt-5.6-codex"
REASONING = "medium"
SANDBOX = "read-only"

BASE_CONTEXT = """You are the CompanyHQ implementation reviewer. Review the current worktree against the supplied plan directory. Inspect the plan files and the implementation changes yourself. Report missed requirements, incorrect interpretation, regressions, security issues, unnecessary scope, and any code smells you find. Rank findings by severity, reference files as path:line, and finish with a short verdict (approve / approve with nits / needs changes). Do not modify any files."""


def repo_root() -> Path:
    proc = subprocess.run(
        ["git", "rev-parse", "--show-toplevel"], capture_output=True, text=True
    )
    if proc.returncode != 0:
        sys.exit("error: review-agent must run inside a git worktree")
    return Path(proc.stdout.strip()).resolve()


def validate_plan_name(value: str) -> str:
    path = Path(value)
    if path.name != value or path.suffix:
        sys.exit("error: --plan must be a plan name without a path or extension")
    return value


def resolve_plan_context(root: Path, plan_name: str) -> tuple[Path, Path]:
    """Resolve the worktree and plan created by the orchestrator for this name."""
    worktree = root.parent / f"{root.name}-worktrees" / plan_name
    if not (worktree / ".git").exists():
        sys.exit(f"error: worktree for plan '{plan_name}' does not exist: {worktree}")

    branch = subprocess.run(
        ["git", "branch", "--show-current"], capture_output=True, text=True, cwd=worktree
    ).stdout.strip()
    if branch != plan_name:
        sys.exit(
            f"error: worktree {worktree} is on branch '{branch or '(detached)'}', "
            f"not '{plan_name}'"
        )

    plan_file = worktree / "Plans" / f"{plan_name}.md"
    if not plan_file.is_file():
        sys.exit(f"error: plan for '{plan_name}' does not exist: {plan_file}")
    return worktree.resolve(), plan_file.resolve()


def build_prompt(worktree: Path, plan_file: Path) -> str:
    relative_plan_file = plan_file.relative_to(worktree)
    return (
        f"{BASE_CONTEXT}\n\n"
        "## Review Scope\n"
        f"- Worktree: {worktree}\n"
        f"- Plan: {relative_plan_file}\n\n"
        "Start by reading the plan. Then inspect "
        "the current worktree and its changes, and review the implementation against "
        "the plan."
    )


def run_reviewer(prompt: str, cwd: Path) -> tuple[bool, str]:
    binary = shutil.which(AGENT)
    if binary is None:
        return False, f"'{AGENT}' not found on PATH"

    fd, temporary_output = tempfile.mkstemp(prefix="review_codex_", suffix=".md")
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
    finally:
        pass

    try:
        if proc.returncode != 0:
            return False, proc.stderr.strip() or proc.stdout.strip() or "(no output)"
        return True, output_file.read_text(encoding="utf-8").strip() if output_file.exists() else proc.stdout.strip()
    finally:
        output_file.unlink(missing_ok=True)


def save_result(ok: bool, output: str, plan_file: Path) -> Path:
    RESULTS_DIR.mkdir(parents=True, exist_ok=True)
    stamp = dt.datetime.now().strftime("%Y%m%d_%H%M%S")
    path = RESULTS_DIR / f"{stamp}_codex.md"
    header = (
        "# Review by Codex\n\n"
        f"- **Model:** {MODEL}\n"
        f"- **Reasoning:** {REASONING}\n"
        f"- **Sandbox:** {SANDBOX}\n"
        f"- **Status:** {'ok' if ok else 'FAILED'}\n"
        f"- **Plan:** {plan_file}\n\n---\n\n"
    )
    path.write_text(header + output + "\n", encoding="utf-8")
    return path


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--plan", required=True, help="orchestrator-created plan name")
    args = ap.parse_args()

    root = repo_root()
    plan_name = validate_plan_name(args.plan)
    worktree, plan_file = resolve_plan_context(root, plan_name)
    print(f"[review-agent] reviewing {plan_file} in {worktree} with {AGENT}/{MODEL} ({SANDBOX})...")
    ok, output = run_reviewer(build_prompt(worktree, plan_file), worktree)
    result = save_result(ok, output, plan_file)
    status = "done" if ok else "FAILED"
    print(f"\n===== codex [{status}] -> {result} =====\n")
    print(output)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
