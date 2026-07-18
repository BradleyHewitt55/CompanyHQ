#!/usr/bin/env python3
"""Run the preconfigured Codex planner for an orchestrator-created worktree.

Usage:
    cms python plan-agent.py --plan tenant-audit-logging --request "Add tenant audit logging"
    cms python plan-agent.py --plan tenant-audit-logging --answers "1. Log to Postgres."

Writes the finished plan to Plans/<plan>.md inside the worktree.
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

# Planning is read-only for Codex; this wrapper writes the plan file after a
# successful run. The orchestrator creates the branch and worktree first.
AGENT = "codex"
MODEL = "gpt-5.6-terra"
REASONING = "high"
SANDBOX = "read-only"

QUESTIONS_MARKER = "## QUESTIONS FOR ORCHESTRATOR"
SESSION_ID_RE = re.compile(
    r"session id:\s*([0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12})", re.IGNORECASE
)

BASE_CONTEXT = f"""You are the CompanyHQ implementation planner. Inspect the current worktree before writing the plan. Consult relevant project documentation and authoritative technology documentation when needed to select the cleanest approach. Your job is to transfer the decisions and codebase knowledge you discovered so the implementation agent does not need to guess or re-explore.

Return a complete, implementation-ready Markdown plan and nothing else; the orchestrator saves your response verbatim as the plan file. Do not modify files, create branches, or create worktrees. Keep the scope limited to the requested work. Use exact paths and stable symbol names, but do not include line numbers or long verbatim code blocks. Describe contracts and signatures rather than supplying implementation code.

Use this exact structure. Omit a section only when it is genuinely inapplicable, and say "None" rather than leaving an applicable section vague:

# <Concise implementation title>

## Goal and success criteria
- State the requested outcome and observable done conditions, including relevant behavior and passing checks.

## Current state
- Explain how the relevant behavior works today.
- Name exact files, key types, symbols, and conventions the implementation relies on.

## Decisions made
- <Decision>: <chosen approach>. Rejected <alternative> because <reason>.

## Non-goals / do not touch
- State the boundaries explicitly, including unrelated refactors and protected areas.

## Data, API, and UI changes
- List schema and migration deltas, wire-format/DTO shapes, configuration keys, routes, and UI changes that cross a boundary.
- Write "None" if this work has no boundary changes.

## Implementation steps
1. **<Step title>**
   - **Files:** `<path>` (new/modify/delete), ...
   - **Changes:** precise implementation work and any dependency on an earlier step.
   - **Contracts:** new or changed public/internal method signatures, DTO fields, configuration keys, route shapes, or persistence mappings.
2. Continue with ordered steps. State ordering dependencies where they matter.

## Tests
- `<test project/path>` — `<test name>`: <scenario>; expected <result>.
- Identify existing tests that must change or are expected to break, if any.

## Gotchas and conventions
- Record surprising couplings, misleading-but-irrelevant files, ordering requirements, and patterns to mirror.

## Recorded assumptions
- List conservative assumptions that do not change the approach, or write "None".

## Verification commands
```powershell
<exact restore/build/test commands for this repository>
```

If you hit a decision you cannot make from the request or the codebase, do not guess: end your response with a section titled '{QUESTIONS_MARKER}' containing numbered questions. For minor ambiguities that do not change the approach, take the most conservative interpretation and record the assumption in the plan."""


def repo_root() -> Path:
    proc = subprocess.run(
        ["git", "rev-parse", "--show-toplevel"], capture_output=True, text=True
    )
    if proc.returncode != 0:
        sys.exit("error: plan-agent must run inside a git worktree")
    return Path(proc.stdout.strip()).resolve()


def validate_plan_name(value: str) -> str:
    path = Path(value)
    if path.name != value or path.suffix:
        sys.exit("error: --plan must be a plan name without a path or extension")
    return value


def resolve_plan_context(root: Path, plan_name: str) -> tuple[Path, Path]:
    """Resolve the worktree created by the orchestrator and the plan file to write."""
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
    if plan_file.exists():
        sys.exit(f"error: plan for '{plan_name}' already exists: {plan_file}")
    return worktree.resolve(), plan_file.resolve()


def session_file(plan_name: str) -> Path:
    return RESULTS_DIR / f"{plan_name}.session"


def build_prompt(request: str) -> str:
    return (
        f"{BASE_CONTEXT}\n\n"
        "## Requested work\n"
        f"{request.strip()}\n\n"
        "## Output\n"
        "Start with a title and return only the plan."
    )


def build_answers_prompt(answers: str) -> str:
    return (
        "## Answers from orchestrator\n"
        f"{answers.strip()}\n\n"
        "Continue planning using these answers and return the complete plan. If you "
        f"are still blocked, end with another '{QUESTIONS_MARKER}' section."
    )


def run_planner(
    prompt: str, cwd: Path, resume_session: str | None = None
) -> tuple[bool, str, str | None]:
    binary = shutil.which(AGENT)
    if binary is None:
        return False, f"'{AGENT}' not found on PATH", None

    fd, temporary_output = tempfile.mkstemp(prefix="plan_codex_", suffix=".md")
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
        "# Plan by Codex\n\n"
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
    ap.add_argument("--request", help="work to plan (first run)")
    ap.add_argument(
        "--answers",
        help="answers to the agent's questions; resumes the stored session for this plan",
    )
    args = ap.parse_args()
    if bool(args.request) == bool(args.answers):
        sys.exit("error: pass exactly one of --request or --answers")

    root = repo_root()
    plan_name = validate_plan_name(args.plan)
    worktree, plan_file = resolve_plan_context(root, plan_name)

    if args.answers:
        stored = session_file(plan_name)
        if not stored.is_file():
            sys.exit(
                f"error: no stored session for plan '{plan_name}'; run with --request first"
            )
        resume_session = stored.read_text(encoding="utf-8").strip()
        prompt = build_answers_prompt(args.answers)
        print(f"[plan-agent] resuming session {resume_session} with answers...")
    else:
        resume_session = None
        prompt = build_prompt(args.request)
        print(
            f"[plan-agent] planning '{args.request}' in {worktree} with {AGENT}/{MODEL} ({REASONING})..."
        )

    ok, output, session_id = run_planner(prompt, worktree, resume_session)
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
            f'\n[plan-agent] agent is blocked; reply with: --plan {plan_name} --answers "..."'
        )
        return 2
    if not ok:
        return 1

    plan_file.parent.mkdir(parents=True, exist_ok=True)
    plan_file.write_text(output + "\n", encoding="utf-8")
    print(f"\n[plan-agent] plan written to {plan_file}; ready for code-agent")
    return 0


if __name__ == "__main__":
    sys.exit(main())
