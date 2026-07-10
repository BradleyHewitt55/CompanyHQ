# Source control

> **Document status:** Target V1 architecture contract.

VCS operations (status, diff, refs, worktrees, commits) are separate from hosted-provider operations (repository metadata, pull requests, checks). Git is the V1 VCS; GitHub is the first hosted adapter, with CompanyHQ-owned interfaces for later providers.

Every process uses fixed argument arrays, canonical working directories, bounded output, timeout/cancellation, sanitized environment, structured errors, and audit. Credentials are adapter references and never returned to agents.

Agents may modify the assigned worktree and, only under explicit policy/approval, prepare commits or push a task branch. They cannot write protected branches, force-push, merge, approve reviews, change branch protection, manage credentials, or deploy.
