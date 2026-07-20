# `companyhq` CLI

A headless host ([`Projects/Cli`][1]) over the same plugins the Admin host loads.
It calls `AddCompanyHqPlugins` but never `UseCompanyHqPlugins` — there is no
pipeline.

## Commands

```text
companyhq --c <CompanyName> [--root <path>]
          [--sip <ip> --u <username> [--p <password> | --conf <key-directory>]]

companyhq service <ServiceName> [--c <CompanyName>] [--root <path>]
```

| Flag | Meaning |
|---|---|
| `--c` | Company name. Required to create; optional for `service`. |
| `--root` | Server root. Defaults to `~/CompanyHQ`. |
| `--sip` | Remote server IP — relays the create flow over SSH. |
| `--u` | SSH username. Required with `--sip`. |
| `--p` | SSH password. Omit for a masked prompt. |
| `--conf` | Directory holding a private key, instead of `--p`. |

## Create a company

Prompts for admin name, surname, email, contact number, password confirmation,
and database placement, then registers the company, provisions the database, and
seeds the `Admin` user. See [Companies](../Architecture/Companies.md).

```powershell
companyhq --c "Acme" --root "$PWD/MockServer"
```

`--sip` relays the identical flow to a remote server over SSH.

## Scaffold a service

**Run it from inside the company directory** and both the company and the server
root are inferred:

```powershell
cd MockServer/Companies/Acme
companyhq service Billing
# Created ...\MockServer\Companies\Acme\Services\Billing
```

[`CompanyLocator`][2] walks up looking for a directory whose parent is
`Companies`. Any depth inside the company works — `Acme/Services/Billing` resolves
to `Acme` just as `Acme` does.

Outside a company directory, pass the company explicitly:

```powershell
companyhq service Billing --c "Acme" --root "$PWD/MockServer"
```

The generated service is a compilable plugin shaped like
[`Projects/Services/Auth`][3] — see
[Service scaffolding](../Architecture/ServiceScaffolding.md).

## Verify it worked

```powershell
cd MockServer/Companies/Acme/Services/Billing
dotnet build
```

The scaffolded project builds with no warnings.

## Common mistakes

- **Running `service` from the `Companies` folder.** That folder is not a company;
  the CLI fails rather than guess a tenant. `cd` into the company.
- **Running `service` from the repo root.** There is no company to infer — pass
  `--c` and `--root`, or `cd` into `MockServer/Companies/{Name}`.
- **Combining `service` with `--sip`.** The verb rejects remote options; SSH relays
  company creation only.
- **Building a scaffolded service outside `MockServer/`.** The default
  `CompanyHqAbstractionsPath` assumes the repo geometry. Set
  `CompanyHq:AbstractionsPath`, or edit the company's
  `Services/Directory.Build.props`.

[1]: ../../Projects/Cli/Program.cs
[2]: ../../Projects/Services/Companies/Services/CompanyLocator.cs
[3]: ../../Projects/Services/Auth
