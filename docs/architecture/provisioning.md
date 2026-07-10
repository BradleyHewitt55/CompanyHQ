# Company provisioning and Docker operation

Provisioning creates an isolated CompanyHQ instance on a server. It is idempotent: rerunning it must inspect and repair the requested company without damaging an existing one.

## Add a server from a local machine

The local CompanyHQ CLI connects to the target server over SSH, verifies it, and performs remote provisioning. The operator does not first open an interactive SSH session on the server.

~~~text
companyhq server add -name <server-profile> -host <hostname-or-ip> -user <ssh-user> \
  [-port <ssh-port>] [-identity <private-key-path>] [-root <remote-companyhq-root>]
~~~

The command must:

1. resolve the host and show the SSH host-key fingerprint;
2. require explicit trust on first connection and reject a changed host key;
3. use the local SSH agent or the named identity file without copying private key material;
4. connect through SSH, test the required remote privileges, and run the idempotent preflight;
5. save only the profile name, host, port, user, verified host-key fingerprint, remote root, and identity reference;
6. return a connection test result and the next company command.

Passwords, private keys, one-time codes, and passphrases are prompted through a secure input channel and are never accepted as saved profile fields or written to shell history. `-identity` points to a local key; it never uploads the key.

Named profiles are managed locally:

~~~text
companyhq server list
companyhq server test -name <server-profile>
companyhq server connect -name <server-profile>
companyhq server remove -name <server-profile>
~~~

`server connect` is an audited convenience command that opens an interactive SSH session using the verified profile. Provisioning commands use the same SSH connection non-interactively.

## Add a company

~~~text
companyhq company add -company <company-name> -server <server-profile> \
  [-config <configuration-directory>]
~~~

- `-company` is the display name collected for the company draft.
- `-server` resolves a previously verified, case-sensitive local SSH profile.
- `-config` selects a local configuration directory when the default is unsuitable.
- Direct password flags are forbidden. Add or test the server profile first.

## Required company interview

After connection and before provisioning, the CLI prompts for:

- company display name and stable company key;
- description;
- target market;
- primary owner;
- operational timezone and region where required by deployment;
- confirmation of the selected server profile.

The prompt is resumable. A cancelled or failed run keeps only the minimum safe draft state needed to resume and never keeps a plaintext password.

## Server preparation

The CLI performs the following visible stages:

~~~text
preflight -> install prerequisites if absent -> create company boundary
-> connect company root repository -> configure secrets -> start Docker composition
-> health check -> handoff
~~~

Preflight probes the server for the declared CompanyHQ runtime prerequisites, Docker, and a usable Docker Compose capability. If a dependency already exists, the installer skips it and continues. If installation is needed, the admin/CLI loading view shows the active stage, current action, retry guidance, and a safe failure state. The run re-probes after installation; it does not assume success.

## Company server layout

The canonical root is:

~~~text
CompanyHQ/
  <company-key>/
    docker-compose/
    .env
~~~

The composition may add managed subdirectories for application workspaces, generated configuration, durable data references, backups, and agent worktrees. Their contents are implementation detail; they must remain inside the company root and never be shared with another tenant.

## Runtime expectations

The website, admin dashboard, and CompanyHQ control plane run as Docker-managed services. Company-scoped agent workers are added only for explicitly enabled provider profiles. Start, restart, health, preview, deploy, and rollback actions operate through the company composition and emit deployment audit events.

The provisioned company connects or creates its CompanyHQ root repository before handoff. This stores tenant admin overrides and custom-service source independently from the supplied base UI. The environment file contains tenant-scoped runtime configuration. It is not a GitHub backup artifact. Git stores an example and encrypted or indirect secret references only.

## Failure and recovery

A failed provision reports the failed stage, inspected host state, safe retry action, and cleanup status. It must not delete a pre-existing company directory, project repository, database, or object storage boundary merely because a retry occurs. Destructive cleanup requires an owner-approved Change.
