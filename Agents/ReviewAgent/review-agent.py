#!/usr/bin/env python3
"""Run the preconfigured Codex reviewer for an orchestrator-created plan.

Usage:
    cms python review-agent.py --plan platform-v3
    cms python review-agent.py --plan platform-v3 --answers "1. The rename is intentional."

Exit codes: 0 done, 1 failed, 2 agent is blocked on questions (answer with --answers).
"""

from __future__ import annotations

import argparse
import datetime as dt
import os
import re
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

QUESTIONS_MARKER = "## QUESTIONS FOR ORCHESTRATOR"
SESSION_ID_RE = re.compile(
    r"session id:\s*([0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12})", re.IGNORECASE
)

BASE_CONTEXT = f"""You are the CompanyHQ implementation reviewer. Review the current worktree against the supplied plan directory. Inspect the plan files and the implementation changes yourself. Report missed requirements, incorrect interpretation, regressions, security issues, unnecessary scope, and any code smells you find. Rank findings by severity, reference files as path:line, and finish with a short verdict (approve / approve with nits / needs changes). Do not modify any files. If you cannot complete the review without a decision from the orchestrator (for example the plan's intent is unclear), do not guess: end your response with a section titled '{QUESTIONS_MARKER}' containing numbered questions."""


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


def session_file(plan_name: str) -> Path:
    return RESULTS_DIR / f"{plan_name}.session"


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


def build_answers_prompt(answers: str) -> str:
    return (
        "## Answers from orchestrator\n"
        f"{answers.strip()}\n\n"
        "Continue the review using these answers and deliver the full findings and "
        f"verdict. If you are still blocked, end with another '{QUESTIONS_MARKER}' section."
    )


def run_reviewer(
    prompt: str, cwd: Path, resume_session: str | None = None
) -> tuple[bool, str, str | None]:
    binary = shutil.which(AGENT)
    if binary is None:
        return False, f"'{AGENT}' not found on PATH", None

    fd, temporary_output = tempfile.mkstemp(prefix="review_codex_", suffix=".md")
    os.close(fd)
    output_file = Path(temporary_output)
    command = [binary, "exec"]
    if resume_session:
        command += ["resume", resume_session]
    command += [
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
        return False, f"timed out after {TIMEOUT_S}s", resume_session

    match = SESSION_ID_RE.search(f"{proc.stdout}\n{proc.stderr}")
    session_id = match.group(1) if match else resume_session
    try:
        if proc.returncode != 0:
            return (
                False,
                proc.stderr.strip() or proc.stdout.strip() or "(no output)",
                session_id,
            )
        return (
            True,
            output_file.read_text(encoding="utf-8").strip()
            if output_file.exists()
            else proc.stdout.strip(),
            session_id,
        )
    finally:
        output_file.unlink(missing_ok=True)


def save_result(status: str, output: str, plan_file: Path) -> Path:
    RESULTS_DIR.mkdir(parents=True, exist_ok=True)
    stamp = dt.datetime.now().strftime("%Y%m%d_%H%M%S")
    path = RESULTS_DIR / f"{stamp}_codex.md"
    header = (
        "# Review by Codex\n\n"
        f"- **Model:** {MODEL}\n"
        f"- **Reasoning:** {REASONING}\n"
        f"- **Sandbox:** {SANDBOX}\n"
        f"- **Status:** {status}\n"
        f"- **Plan:** {plan_file}\n\n---\n\n"
    )
    path.write_text(header + output + "\n", encoding="utf-8")
    return path


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--plan", required=True, help="orchestrator-created plan name")
    ap.add_argument(
        "--answers",
        help="answers to the agent's questions; resumes the stored session for this plan",
    )
    args = ap.parse_args()

    root = repo_root()
    plan_name = validate_plan_name(args.plan)
    worktree, plan_file = resolve_plan_context(root, plan_name)

    if args.answers:
        stored = session_file(plan_name)
        if not stored.is_file():
            sys.exit(
                f"error: no stored session for plan '{plan_name}'; run without --answers first"
            )
        resume_session = stored.read_text(encoding="utf-8").strip()
        prompt = build_answers_prompt(args.answers)
        print(f"[review-agent] resuming session {resume_session} with answers...")
    else:
        resume_session = None
        prompt = build_prompt(worktree, plan_file)
        print(
            f"[review-agent] reviewing {plan_file} in {worktree} with {AGENT}/{MODEL} ({SANDBOX})..."
        )

    ok, output, session_id = run_reviewer(prompt, worktree, resume_session)
    if session_id:
        RESULTS_DIR.mkdir(parents=True, exist_ok=True)
        session_file(plan_name).write_text(session_id + "\n", encoding="utf-8")

    blocked = ok and QUESTIONS_MARKER.lower() in output.lower()
    status = "NEEDS CLARIFICATION" if blocked else ("ok" if ok else "FAILED")
    result = save_result(status, output, plan_file)
    print(f"\n===== codex [{status}] -> {result} =====\n")
    print(output)
    if blocked:
        print(
            f'\n[review-agent] agent is blocked; reply with: --plan {plan_name} --answers "..."'
        )
        return 2
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
