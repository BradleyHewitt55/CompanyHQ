import json
import subprocess
import sys
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from Agents.CodeReviewAgent.code_review_agent import (
    EFFORT,
    MODEL,
    PERMISSION_MODE,
    _issue_prompt,
    run_claude,
)


class CodeReviewAgentTests(unittest.TestCase):
    def test_issue_prompt_contains_entire_issue(self):
        body = (
            "Original request.\n"
            "<!-- agent-plan:start -->\n"
            "# Review feature plan\n\nApproved steps.\n"
            "<!-- agent-plan:end -->\n"
            "Final decision."
        )
        issue = {
            "number": 51,
            "title": "Review feature",
            "url": "https://example.test/issues/51",
            "body": body,
        }

        prompt = _issue_prompt(issue)

        self.assertIn("Review code for GitHub issue #51: Review feature", prompt)
        self.assertIn(f"BEGIN ENTIRE GITHUB ISSUE\n{body}\nEND ENTIRE GITHUB ISSUE", prompt)

    @patch("Agents.CodeReviewAgent.code_review_agent.subprocess.run")
    @patch("Agents.CodeReviewAgent.code_review_agent.shutil.which")
    def test_run_claude_uses_frozen_preset_and_parses_review(self, which, run):
        which.return_value = "claude"
        run.return_value = subprocess.CompletedProcess(
            [], 0, json.dumps({
                "session_id": "session-1",
                "result": "STATUS: CODE_REVIEW\n## Findings\nNo findings.",
            }), "")

        result = run_claude("prompt", None)

        self.assertEqual("CODE_REVIEW", result["status"])
        command = run.call_args.args[0]
        self.assertEqual(MODEL, command[command.index("--model") + 1])
        self.assertEqual(EFFORT, command[command.index("--effort") + 1])
        self.assertEqual(
            PERMISSION_MODE,
            command[command.index("--permission-mode") + 1],
        )


if __name__ == "__main__":
    unittest.main()
