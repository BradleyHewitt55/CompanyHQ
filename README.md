# CompanyHQ

Shared C# services, dependency update tooling, and a minimal company starter.

```powershell
node .\Scripts\companyhq.mjs create --name "My Company" --path D:\Projects\MyCompany
cd D:\Projects\MyCompany
.\Scripts\StartDev.ps1
```

Creation installs exactly StartDev.ps1, StartProd.ps1, Update.ps1 and Scripts/AGENTS.md.
StartDev defaults to --Web on the host with API/infrastructure in Docker; StartProd starts all in Docker.
The destination must be empty; use --port-offset 100 if another starter occupies the default ports.
Copy .env.example to .env or .env.dev and configure admin credentials before the first start.

Inside the company, Update pulls the saved upstream and records its exact commit. Rebuild afterward.
Without Update it continues using the selected version. A first update after a fresh clone restores the
locked revision; a second advances to latest. Company-owned apps, configuration and scripts survive updates.
Edited dependencies and non-forward upstream history stop the update. Commit the dependency lock file.
HQ must be committed and pushed before other users can fetch changes from GitHub.

Implementations and client Contracts are source ProjectReferences, not published HQ NuGet packages.
C# clients reference Contracts; TypeScript types are generated from service OpenAPI documents.
An existing Blazor/company website can keep its UI and call the API without adopting the starter website.
See [the consumer guide](Templates/Company/README.md) for DTO references, Blazor calls and admin seeding.

Verification tooling belongs to HQ, not generated company folders. Run node Scripts/test-companyhq-cli.mjs
only when validating creation/update changes. No consumer artifacts directory is created by startup.
