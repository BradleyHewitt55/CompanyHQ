# Project task workflow

> **Document status:** Target V1 architecture contract.

```text
approved task -> create task branch/worktree -> claim run -> plan
-> edit/test/checkpoint -> evidence/diff -> human review
-> optional approved commit -> optional approved push/PR -> human merge outside agent authority
```

Checkpoints capture filesystem/VCS state separately from provider conversation state. Revert creates a new auditable task event and never rewrites protected history. Completion means acceptance criteria were met; it does not imply merge or deployment.
