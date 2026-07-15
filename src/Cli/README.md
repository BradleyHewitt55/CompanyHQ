# CLI

The thin `companyhq` bootstrap command. It remains a standalone project because `companyhq new` must provision a server over SSH and start Docker before any CompanyHQ application exists to serve an API. Service generation delegates to the Scaffolding service library.

## Create a company

```powershell
dotnet run --project src/Cli -- new --name "Acme Industries" --username admin
```

The default output root is `CompanyHQ/Companies`. The command normalizes the initial name once and creates the deployment at:

```text
CompanyHQ/Companies/acme-industries/
```

That slug is stored in `company.json` and registered with the server. It is immutable: changing the display name never renames or moves the deployment directory.

Useful options:

| Option | Purpose |
|---|---|
| `--database <provider>` | Database provider; defaults to `postgres`. |
| `--output <directory>` | Local company-root override; defaults to `CompanyHQ/Companies`. |
| `--no-start` | Scaffold only; do not start Compose or bootstrap an owner. |
| `--server-ip <ip>` | Upload and start on a server through SSH. |
| `--config <directory>` | Directory containing `id_rsa` for SSH authentication. |
| `--username <name>` | Bootstrap the initial company owner after startup. |

The legacy aliases `-db`, `-sip`, and `-conf` map to `--database`, `--server-ip`, and `--config`.

## Server registration and deletion

Set `COMPANYHQ_ROOT_URL` and `COMPANYHQ_ROOT_TOKEN` when creating a company to register its name, immutable slug, host, and admin URL with the server registry.

The registry host must set `Companies:Root` to its trusted deployment root (normally `CompanyHQ/Companies`). The deletion flow resolves the directory from that setting and the stored slug; it never accepts a path from a browser request.

Deletion is deliberately not a CLI command. An authenticated **Owner** must delete the company from the registry UI, type the exact company name, and accept the browser confirmation. The server stops that company's Compose deployment, removes only its directory and deployment artifacts, then removes its registry record. Admins, members, viewers, agents, and unauthenticated callers cannot delete a company.

## Create a service

```powershell
dotnet run --project src/Cli -- new-service --name Invoices --parent Sales
```

`new-service` requires a PascalCase name and an owning parent service. Use `--output` to select the target company source tree.
