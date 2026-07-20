import json
import subprocess
import sys
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from Agents.CodeAgent.code_agent import (
    EFFORT,
    MODEL,
    SANDBOX_MODE,
    _issue_prompt,
    run_codex,
)


class CodeAgentTests(unittest.TestCase):
    def test_issue_prompt_contains_entire_issue_and_managed_plan(self):
        body = (
            "Original request and constraints.\n"
            "<!-- agent-plan:start -->\n"
            "# Implement feature plan\n\nApproved steps.\n"
            "<!-- agent-plan:end -->\n"
            "Final issue decision."
        )
        issue = {
            "number": 42,
            "title": "Implement feature",
            "url": "https://example.test/issues/42",
            "body": body,
        }

        prompt = _issue_prompt(issue)

        self.assertIn("Implement GitHub issue #42: Implement feature", prompt)
        self.assertIn(f"BEGIN ENTIRE GITHUB ISSUE\n{body}\nEND ENTIRE GITHUB ISSUE", prompt)

    @patch("Agents.CodeAgent.code_agent.subprocess.run")
    @patch("Agents.CodeAgent.code_agent.shutil.which")
    def test_run_codex_uses_frozen_preset_and_parses_completion(self, which, run):
        which.return_value = "codex"
        events = (
            json.dumps({"type": "thread.started", "thread_id": "thread-1"}) + "\n" +
            json.dumps({
                "type": "item.completed",
                "item": {
                    "type": "agent_message",
                    "text": "STATUS: IMPLEMENTATION\n## Summary\nDone.",
                },
            })
        )
        run.return_value = subprocess.CompletedProcess([], 0, events, "")

        result = run_codex("issue", None)

        self.assertEqual("IMPLEMENTATION", result["status"])
        self.assertEqual("thread-1", result["session_id"])
        command = run.call_args.args[0]
        self.assertEqual(MODEL, command[command.index("--model") + 1])
        self.assertIn(f'model_reasoning_effort="{EFFORT}"', command)
        self.assertEqual(SANDBOX_MODE, command[command.index("--sandbox") + 1])


if __name__ == "__main__":
    unittest.main()
