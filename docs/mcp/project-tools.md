# MCP project tools

> **Document status:** Target V1 tool contract.

Project tools expose registered project metadata, health, allowed scripts, task worktree status, bounded files, diffs, and approved test/build commands. All path inputs are project-relative, canonicalized after symlink resolution, and constrained to the assigned worktree.

Command execution selects a registered script ID with fixed executable/arguments; arbitrary shell strings are forbidden. Output, duration, environment, resources, and artifact paths are bounded and audited.
