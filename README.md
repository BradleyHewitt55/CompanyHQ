# CompanyHQ

Shared C# services published as NuGet packages, and a minimal company starter that consumes them.

```powershell
npx github:BradleyHewitt55/CompanyHQ create --name "My Company" --path D:\Projects\MyCompany
cd D:\Projects\MyCompany
.\Scripts\StartDev.ps1
```

No HQ checkout is needed: npx downloads this repository, so Git must be signed in to an account that
can read it. From a checkout, node .\Scripts\companyhq.mjs create does the same.
Creation installs exactly StartDev.ps1, StartProd.ps1, Update.ps1 and Scripts/AGENTS.md.
StartDev defaults to --Web on the host with API/infrastructure in Docker; StartProd starts all in Docker.
Both write the company's database migrations whenever its model changed, before building.
The destination must be empty; use --port-offset 100 if another starter occupies the default ports.
Fill in .env.dev before the first StartDev, and .env before the first StartProd.

A company holds no HQ source. It references the CompanyHQ packages on GitHub Packages at the one version
pinned in its Directory.Build.props; its Update script moves that pin to the newest release or to any
older one. A release is a vX.Y.Z tag pushed here, which publishes every package at that version.
Published versions never change, so a company that does not update keeps building.

C# clients reference the Contracts packages; TypeScript types are generated from OpenAPI documents.
An existing Blazor/company website can keep its UI and call the API without adopting the starter website.
See [the consumer guide](Templates/Company/README.md) for packages, migrations, contracts and admin seeding.

Verification tooling belongs to HQ, not generated company folders. Run node Scripts/test-companyhq-cli.mjs
only when validating creation changes. No consumer artifacts directory is created by startup.
