# GitHub service

`Services/GitHub` (`CompanyHQ.GitHub`) connects one GitHub repository to the deployment,
files issues in it for other services, and lets staff read, comment on and close them. It owns the
single `GitHubConnection` row and the encryption of its access token. It does not own the form that
collects a report (that is `Widget`), the staff inbox the issues appear in (that is `Inbox`), the
OAuth flow that obtains a token, or anything about the company profile.

## Rules a change must not break

- The connection is a singleton keyed by `GitHubConnection.SingletonId`. Never insert a second row.
- The access token is stored only as AES-GCM ciphertext and is in no DTO, log, or response. The
  manager's view names the repository; everyone else learns only whether issues can be filed.
- A repository is verified with GitHub before it is stored: the token must be able to read it and
  Issues must be enabled. GitHub's own spelling of the name is what gets saved.
- Every route but `GET /api/github` needs `github.manage`, checked by the policy and again in the
  service, which answers `Forbidden` to an in-process caller without it.
- **There is no anonymous route.** `SubmitIssueAsync` takes no acting user and is reached only in
  process, by a bridge from the service that collected the report. That service owns the rate
  limit. Everything in `SubmitGitHubIssueRequest` is that service's anonymous public input: the
  email and origin are echoed into the issue body and are never an identity.
- Only issues are served. A pull request answers on GitHub's issue routes too and is a 404 here.

## Decisions, and what was not built

- **It references no feature service.** The connection lives in its own table rather than on the
  company profile, so GitHub plugs in or out without Company knowing.
- **The key is optional.** With no `GitHub:TokenEncryptionKey` the host starts, connecting answers
  503, and `GET /api/github` reports issues as unavailable. A key that is set but is not 32 bytes of
  base64 stops startup.
- **One error enum, mapped once.** A transport failure or timeout is 502, a missing key or missing
  connection is 503, and a repository GitHub refuses at connect time is 400.
- **The issue list goes through GitHub's search API**, the one listing that carries a total and
  leaves pull requests out. It is most recently updated first, and `keyword` is GitHub's own search.
  A `field` is ignored: the rows are not in Postgres.
- **Every call goes through `Octokit`, GitHub's official client.** The token is data, not host
  configuration, so the service builds a client per call on a pooled handler; there is no
  host-registered GitHub client and no hand-written request code.
- **Nothing is cached or stored.** Each read is a live call with the stored token, so a comment made
  on GitHub shows on the next read.
- No webhooks, no labels or assignees, no reopening, no issue deletion (GitHub offers none to a
  repository token), no per-user GitHub identity, and no token refresh: a GitHub App with expiring
  user tokens cannot be used. A comment is posted as the token's own GitHub account.

## Traps and wiring

- Reading, commenting and closing need a token that can write issues, which `ConnectAsync` does not
  verify: it only checks the repository is readable with Issues on. A read-only token connects and
  then answers 502 on a comment.
- Closing reads the issue first and sends it back with the state changed. Octokit's bare
  `IssueUpdate` always sends a null milestone, which would clear the issue's milestone.
- Rotating `GitHub:TokenEncryptionKey` strands the stored token. Submission then answers 503 until
  the repository is connected again; nothing re-encrypts.
- The admin console's OAuth flow (`Apps/Web/app/api/admin/github`) only obtains a token and calls
  `PUT /api/github`. Its `GITHUB_CLIENT_ID`, `GITHUB_CLIENT_SECRET` and `GITHUB_CALLBACK_URL` are Web
  settings, not this service's.
- The folder is `GitHub` but the generated document is `github.json`; `Scripts/openapi.mjs` matches
  the folder without case for that reason.
