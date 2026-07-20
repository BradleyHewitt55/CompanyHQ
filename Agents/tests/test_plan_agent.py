import sys
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from Agents.PlanAgent.plan_agent import LOCAL_SYSTEM_PROMPT, main


class PlanAgentTests(unittest.TestCase):
    @patch("Agents.PlanAgent.plan_agent.update_plan")
    @patch("Agents.PlanAgent.plan_agent.read_issue")
    @patch("Agents.PlanAgent.plan_agent.run_claude")
    def test_local_passes_prompt_without_github_side_effects(
            self, run, read_issue, update_plan):
        run.return_value = {
            "session_id": "session-1",
            "status": "PLAN",
            "output": "STATUS: PLAN",
            "exit_code": 0,
        }

        with patch.object(
                sys, "argv", ["plan_agent.py", "local", "Plan this task"]):
            exit_code = main()

        self.assertEqual(0, exit_code)
        run.assert_called_once_with("Plan this task", None, LOCAL_SYSTEM_PROMPT)
        read_issue.assert_not_called()
        update_plan.assert_not_called()


if __name__ == "__main__":
    unittest.main()
