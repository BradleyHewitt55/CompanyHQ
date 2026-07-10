# ADR-016: One task worktree and no agent merge

> **Status:** Accepted. **Review:** 2027-01-15.

## Context and decision
Concurrent agents need isolated source state and clear ownership. One parent task owns one worktree shared by its subtasks; agents never merge.

## Alternatives considered
Worktree per subtask; shared project checkout; autonomous PR merge.

## Advantages, disadvantages, and risks
Review and recovery are coherent. Shared-subtask conflicts require serialization/coordination; a worktree lease and task graph handle this.

## Consequences and migration
Commit/push/PR are separately gated. Protected main is never mounted writable for agents.
