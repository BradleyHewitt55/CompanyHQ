import json
import subprocess
import sys
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from Agents.PlanReviewAgent.plan_review_agent import (
    EFFORT,
    MODEL,
    PERMISSION_MODE,
    _issue_prompt,
    run_claude,
)


class PlanReviewAgentTests(unittest.TestCase):
    def test_issue_prompt_preserves_request_and_separates_plan(self):
        issue = {
            "number": 12,
            "title": "Keep exact decisions",
            "url": "https://example.test/issues/12",
            "body": (
                "Request line 1\nDecision: retain  two spaces.\n"
                "<!-- agent-plan:start -->\n"
                "## Implementation plan\n# Candidate plan\n\nDo work.\n"
                "<!-- agent-plan:end -->\nPostscript decision."
            ),
        }

        prompt = _issue_prompt(issue)

        self.assertIn(
            "BEGIN EXACT ISSUE REQUEST AND DECISIONS\n"
            "Request line 1\nDecision: retain  two spaces.\n\n"
            "Postscript decision.\n"
            "END EXACT ISSUE REQUEST AND DECISIONS",
            prompt,
        )
        self.assertIn(
            "BEGIN PROPOSED IMPLEMENTATION PLAN\n"
            "# Candidate plan\n\nDo work.\n"
            "END PROPOSED IMPLEMENTATION PLAN",
            prompt,
        )

    @patch("Agents.PlanReviewAgent.plan_review_agent.subprocess.run")
    @patch("Agents.PlanReviewAgent.plan_review_agent.shutil.which")
    def test_run_claude_uses_frozen_preset_and_parses_review(self, which, run):
        which.return_value = "claude"
        run.return_value = subprocess.CompletedProcess(
            [], 0, json.dumps({
                "session_id": "session-1",
                "result": "STATUS: PLAN_REVIEW\n## Findings\nNo findings.",
            }), "")

        result = run_claude("prompt", None)

        self.assertEqual("PLAN_REVIEW", result["status"])
        command = run.call_args.args[0]
        self.assertEqual(MODEL, command[command.index("--model") + 1])
        self.assertEqual(EFFORT, command[command.index("--effort") + 1])
        self.assertEqual(
            PERMISSION_MODE,
            command[command.index("--permission-mode") + 1],
        )


if __name__ == "__main__":
    unittest.main()
