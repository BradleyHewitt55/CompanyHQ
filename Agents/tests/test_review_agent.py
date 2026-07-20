import sys
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from Agents.ReviewAgent.review_agent import LOCAL_SYSTEM_PROMPT, main


class ReviewAgentTests(unittest.TestCase):
    @patch("Agents.ReviewAgent.review_agent.run_codex")
    def test_local_passes_prompt_without_reading_github(self, run):
        run.return_value = {
            "session_id": "thread-1",
            "status": "REVIEW",
            "output": "STATUS: REVIEW",
            "exit_code": 0,
        }

        with patch.object(
                sys, "argv", ["review_agent.py", "local", "Verify task.md"]):
            exit_code = main()

        self.assertEqual(0, exit_code)
        run.assert_called_once_with(
            "Verify task.md",
            session_id=None,
            system_prompt=LOCAL_SYSTEM_PROMPT,
            prompt_heading="",
        )


if __name__ == "__main__":
    unittest.main()
