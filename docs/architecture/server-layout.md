# Company server layout

> **Document status:** Confirmed Target V1 filesystem contract.

```text
CompanyHQ/
  CompanyName/
    admin/                 # installation-owned admin override source
    app/                   # checked-out CompanyHQ application/release metadata
    docker-compose/        # generated Compose and versioned templates
    extensions/            # installation-owned extension source
    projects/              # registered project repositories
    storage/               # local storage only when selected; never shared
    tasks/                 # durable task artifacts, not authoritative DB state
    worktrees/             # one worktree per parent task
    .env                   # protected runtime configuration; never committed
```

All resolved paths must remain beneath the canonical installation root after symlink resolution. Containers receive only required subpaths. Agent containers receive the assigned task worktree and bounded artifact directories, never `.env`, Docker socket, database volume, sibling projects, or application source outside scope.

Directory creation is idempotent and recorded in the provisioning journal. Upgrade and rollback never delete unknown or user-owned files.
