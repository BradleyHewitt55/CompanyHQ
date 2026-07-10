# Git worktrees

> **Document status:** Confirmed Target V1 contract.

One parent task owns one worktree named from stable task ID, never user-controlled free text. Source-changing subtasks share it. Creation occurs from an approved base ref after cleanliness/fetch checks and records repository identity, base commit, branch, canonical path, and owner.

A per-worktree lease serializes source-changing agent runs. Canonical path and symlink checks precede every file/tool operation. Cleanup requires terminal task state, no active lease/session, captured diff/evidence, remote/commit reconciliation, and retention expiry; uncertain worktrees are quarantined.
