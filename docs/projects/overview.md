# Projects

> **Document status:** Target V1 architecture contract.

A project is a registered source repository and optional deployable application owned by the installation. Registration stores stable ID, path, repository identity, default branch, source-control adapter, environments, scripts, access policy, and health—not credentials.

Project paths must resolve beneath `projects/`; task worktrees beneath `worktrees/`. CompanyHQ detects VCS capabilities through an adapter and does not assume every directory is Git.
