# Source-control boundaries

> **Document status:** Confirmed Target V1 security contract.

Repository registration pins canonical root and remote identity. All operations use argument arrays, sanitized environment, bounded output/time, no credential echo, task worktree lease, and protected-ref policy. Symlinks and submodules are treated as escape risks and validated explicitly.

Agents never merge, approve reviews, force-push, alter remotes/protection, expose tokens, or delete repositories. Human approval binds the exact diff/base/head and expires when either changes.
