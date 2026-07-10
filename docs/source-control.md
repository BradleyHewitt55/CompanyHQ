# Source control and version control

CompanyHQ uses Git version control for every source-affecting change. GitHub is the first connected provider; the source-control layer is adapter-based so later providers can implement the same repository, branch, commit, pull-request, status, and permission contract.

## Repository topology

| Repository | Owns |
| --- | --- |
| CompanyHQ product root repository | The supplied platform/base admin implementation, platform service modules, contract, and shared governance. It is not exposed as tenant source. |
| Company root repository, one per company | Tenant admin overrides, custom service source, company-level configuration declarations, and Change history. The Explorer opens its protected main by default. |
| Project repository, one per independently deployable website, mobile app, desktop app, or other product | That project's source, branch history, previews, releases, and deployment declarations. |

The CompanyHQ product root is itself version-controlled. A company root repository is also required so a user's admin UI changes are saved independently of the supplied base. Do not put all tenants' overrides in one shared repository.

## Provider adapter

GitHub is the only initial provider, but CompanyHQ does not hard-code GitHub concepts into service/page contracts. A provider adapter must support:

- repository connection and ownership verification;
- protected default branch, branch creation, commit, diff, and status;
- pull request/review/merge lifecycle where the provider supports it;
- least-privilege, short-lived installation credentials;
- audit links, webhook/event ingestion, retries, and revocation.

An agent never receives a user's personal provider credential or an unrestricted repository token.

## Worktrees, branches, and pull requests

The Explorer displays protected main by default. Editing manually or assigning an agent creates a Change, a branch, and an isolated worktree in the owning company/project repository.

The visible default worktree label is:

~~~text
UsersName_{Task}
~~~

The filesystem key is normalized and collision-safe, using the same user/task meaning plus an internal task identifier. Raw display names are never used as trusted paths.

~~~text
Change -> UsersName_{Task} worktree -> diff/preview/tests
-> commit -> GitHub pull request -> required approval -> merge to main -> deploy
~~~

Agents may change only their assigned worktree. They cannot write main, another worktree, the supplied base repository, or a repository outside their delegated scope. Commits, pushes, pull requests, merges, previews, and production promotion each remain policy/approval decisions.

## Explorer integration

The Explorer shows:

- main branch by default and an active worktree only after deliberate selection;
- tenant-owned page override/source files, custom services, and project files;
- branch, worktree, diff, revision, pull request, review, deployment, test, and rollback state;
- manual or delegated-agent attribution.

For supplied base pages, it shows visual and DTO/component contracts rather than source code or source diffs. A direct Explorer edit always creates a Change/worktree before source is written.

## Backup boundary

GitHub is authoritative for source, service definitions, migrations, tenant overrides, project code, and non-secret deployment declarations. It is not the whole-company backup.

| Asset | Required backup approach |
| --- | --- |
| Operational records and editable content | Encrypted company database backups and restore verification. |
| Media and generated artifacts | Object-storage lifecycle/replication or export with restore verification. |
| Secrets | Encrypted secret recovery procedure; never plaintext Git. |
| Deployment metadata/configuration references | Database/configuration backup and recovery drill. |

A restore reconstructs company composition, configuration references, database, media, projects, source revisions, and audit evidence. Restoring only a Git repository is not a company restore.
