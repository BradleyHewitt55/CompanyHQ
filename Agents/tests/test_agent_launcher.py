import contextlib
import io
import subprocess
import sys
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from Agents.agent_launcher import main


class AgentLauncherTests(unittest.TestCase):
    @patch("Agents.agent_launcher.subprocess.run")
    def test_new_dispatches_to_plan_agent(self, run):
        run.return_value = subprocess.CompletedProcess([], 0)

        exit_code = main("new", ["PlanAgent", "123"])

        self.assertEqual(0, exit_code)
        command = run.call_args.args[0]
        self.assertEqual(sys.executable, command[0])
        self.assertTrue(command[1].endswith("Agents/PlanAgent/plan_agent.py") or
                        command[1].endswith("Agents\\PlanAgent\\plan_agent.py"))
        self.assertEqual(["new", "123"], command[2:])

    @patch("Agents.agent_launcher.subprocess.run")
    def test_new_dispatches_to_plan_review_agent(self, run):
        run.return_value = subprocess.CompletedProcess([], 0)

        exit_code = main("new", ["PlanReviewAgent", "123"])

        self.assertEqual(0, exit_code)
        command = run.call_args.args[0]
        self.assertTrue(
            command[1].endswith(
                "Agents/PlanReviewAgent/plan_review_agent.py") or
            command[1].endswith(
                "Agents\\PlanReviewAgent\\plan_review_agent.py"))
        self.assertEqual(["new", "123"], command[2:])

    @patch("Agents.agent_launcher.subprocess.run")
    def test_new_dispatches_to_code_agent(self, run):
        run.return_value = subprocess.CompletedProcess([], 0)

        exit_code = main("new", ["CodeAgent", "123"])

        self.assertEqual(0, exit_code)
        command = run.call_args.args[0]
        self.assertTrue(command[1].endswith("Agents/CodeAgent/code_agent.py") or
                        command[1].endswith("Agents\\CodeAgent\\code_agent.py"))
        self.assertEqual(["new", "123"], command[2:])

    @patch("Agents.agent_launcher.subprocess.run")
    def test_local_dispatches_prompt_to_each_agent(self, run):
        run.return_value = subprocess.CompletedProcess([], 0)
        agents = {
            "PlanAgent": "PlanAgent/plan_agent.py",
            "PlanReviewAgent": "PlanReviewAgent/plan_review_agent.py",
            "CodeAgent": "CodeAgent/code_agent.py",
            "CodeReviewAgent": "CodeReviewAgent/code_review_agent.py",
            "ReviewAgent": "ReviewAgent/review_agent.py",
        }

        for agent, script in agents.items():
            with self.subTest(agent=agent):
                exit_code = main("local", [agent, "Handle this prompt"])

                self.assertEqual(0, exit_code)
                command = run.call_args.args[0]
                self.assertTrue(
                    command[1].replace("\\", "/").endswith(
                        f"Agents/{script}"))
                self.assertEqual(["local", "Handle this prompt"], command[2:])

    @patch("Agents.agent_launcher.subprocess.run")
    def test_new_dispatches_to_code_review_agent(self, run):
        run.return_value = subprocess.CompletedProcess([], 0)

        exit_code = main("new", ["CodeReviewAgent", "123"])

        self.assertEqual(0, exit_code)
        command = run.call_args.args[0]
        self.assertTrue(
            command[1].endswith(
                "Agents/CodeReviewAgent/code_review_agent.py") or
            command[1].endswith(
                "Agents\\CodeReviewAgent\\code_review_agent.py"))
        self.assertEqual(["new", "123"], command[2:])

    @patch("Agents.agent_launcher.subprocess.run")
    def test_reply_dispatches_to_review_agent_and_preserves_exit_code(self, run):
        run.return_value = subprocess.CompletedProcess([], 7)

        exit_code = main(
            "reply", ["ReviewAgent", "session", "123", "answers"])

        self.assertEqual(7, exit_code)
        command = run.call_args.args[0]
        self.assertTrue(command[1].endswith("Agents/ReviewAgent/review_agent.py") or
                        command[1].endswith("Agents\\ReviewAgent\\review_agent.py"))
        self.assertEqual(["reply", "session", "123", "answers"], command[2:])

    @patch("Agents.agent_launcher.subprocess.run")
    def test_unknown_agent_fails_without_starting_a_process(self, run):
        stderr = io.StringIO()

        with contextlib.redirect_stderr(stderr):
            exit_code = main("new", ["MissingAgent", "123"])

        self.assertEqual(2, exit_code)
        self.assertIn("Unknown agent: MissingAgent", stderr.getvalue())
        run.assert_not_called()

    @patch("Agents.agent_launcher.subprocess.run")
    def test_missing_agent_fails_without_starting_a_process(self, run):
        stderr = io.StringIO()

        with contextlib.redirect_stderr(stderr):
            exit_code = main("new", [])

        self.assertEqual(2, exit_code)
        self.assertIn("Usage: python new <AGENT>", stderr.getvalue())
        run.assert_not_called()


if __name__ == "__main__":
    unittest.main()
