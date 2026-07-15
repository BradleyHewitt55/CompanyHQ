#!/usr/bin/env python3
"""Run the preconfigured Codex implementer for an orchestrator-created plan.

Usage:
    cms python code-agent.py --plan platform-v3
    cms python code-agent.py --plan platform-v3 --answers "1. Reuse OrdersModule."

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

# This is intentionally not a CLI option. A code agent applies the plan inside
# its own worktree, so it needs write access there and nowhere else.
AGENT = "codex"
MODEL = "gpt-5.6-terra"
REASONING = "medium"
SANDBOX = "workspace-write"

QUESTIONS_MARKER = "## QUESTIONS FOR ORCHESTRATOR"
SESSION_ID_RE = re.compile(
    r"session id:\s*([0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12})", re.IGNORECASE
)

BASE_CONTEXT = f"""You are the CompanyHQ implementer. Apply the supplied plan to the current worktree. Read the plan first, then implement every step exactly as written; do not add scope beyond the plan. Keep the code clean and consistent with the surrounding codebase, and build or test what you changed where practical. Do not commit, create branches, or touch anything outside this worktree. Finish with a short summary of what was implemented and anything you could not complete.

## Code rules
- Do not edit `.repos`.
- Do not look into `.repos`.
- When working with the Blazor framework or creating services and you get stuck, use the .NET Core, Entity Framework Core, and ASP.NET Core documentation at `Docs/DotNetDocs`, `Docs/EntityFrameworkDocs`, and `Docs/AspNetCoreDocs`.
- When working with Google Merchant information and you get stuck, use the Google Merchant documentation at `Docs/MerchantApiDocs`.
- If you need a paragraph-long comment to justify why a workaround is okay, the code is wrong, fix it.
- If you hit a decision you cannot make from the plan or the codebase, stop: make no speculative changes and end your response with a section titled '{QUESTIONS_MARKER}' containing numbered questions. For minor ambiguities that do not block progress, take the most conservative interpretation and record the assumption in your summary."""


def repo_root() -> Path:
    proc = subprocess.run(
        ["git", "rev-parse", "--show-toplevel"], capture_output=True, text=True
    )
    if proc.returncode != 0:
        sys.exit("error: code-agent must run inside a git worktree")
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
        "## Implementation Scope\n"
        f"- Worktree: {worktree}\n"
        f"- Plan: {relative_plan_file}\n\n"
        "Start by reading the plan. Then implement it in the current worktree, "
        "following the code rules above."
    )


def build_answers_prompt(answers: str) -> str:
    return (
        "## Answers from orchestrator\n"
        f"{answers.strip()}\n\n"
        "Continue implementing the plan using these answers. If you are still "
        f"blocked, end with another '{QUESTIONS_MARKER}' section."
    )


def run_implementer(
    prompt: str, cwd: Path, resume_session: str | None = None
) -> tuple[bool, str, str | None]:
    binary = shutil.which(AGENT)
    if binary is None:
        return False, f"'{AGENT}' not found on PATH", None

    fd, temporary_output = tempfile.mkstemp(prefix="code_codex_", suffix=".md")
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
        "# Implementation by Codex\n\n"
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
        print(f"[code-agent] resuming session {resume_session} with answers...")
    else:
        resume_session = None
        prompt = build_prompt(worktree, plan_file)
        print(
            f"[code-agent] implementing {plan_file} in {worktree} with {AGENT}/{MODEL} ({SANDBOX})..."
        )

    ok, output, session_id = run_implementer(prompt, worktree, resume_session)
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
            f'\n[code-agent] agent is blocked; reply with: --plan {plan_name} --answers "..."'
        )
        return 2
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
