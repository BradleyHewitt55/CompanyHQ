# CompanyHQ

Shared C# services published as NuGet packages, and a headless company starter that consumes them.

```powershell
npx github:BradleyHewitt55/CompanyHQ create --name "My Company"
cd MyCompany
.\Scripts\StartDev.ps1
```

No HQ checkout is needed: npx downloads this repository, so Git must be signed in to an account that
can read it. From a checkout, node .\Scripts\companyhq.mjs create does the same.

The starter is the company's own plain ASP.NET API. It references the generic packages it uses directly,
beginning with CompanyHQ.Rss and CompanyHQ.Rss.Contracts, registered with AddRss for
in-process use (no RSS route), and maps /health and, in Development, OpenAPI. It has no CompanyHQ.Api
host package, demo providers, test project, website, assets, database or authentication. The company
adds modules by referencing their packages at its pin and registering them in its own Program.cs, and
owns whatever each module needs from its host. See [the consumer guide](Templates/Company/README.md).

Creation installs exactly StartDev.ps1, StartProd.ps1, Update.ps1 and Scripts/AGENTS.md. StartDev starts
the API in Docker and returns; --{project} additionally runs a company-owned web app on the host if the
company has added one. StartProd starts everything in docker-compose.yml in Docker.
The project lands in a folder named after the company in the current directory, or at --path. The
destination must be empty; use --port-offset 100 if another starter occupies the default port.
Configuration is environment-only: .env.dev for StartDev and .env, created with the same keys empty,
for StartProd.
Creation needs no token: the packages are public on nuget.org. It pins the newest stable version
present in the generic CompanyHQ.Rss package feed,
as Update does, and fails if no release is available. Tags alone do not establish package availability,
and CompanyHQ.Api is never consulted.

A company holds no HQ source. It references the CompanyHQ packages on nuget.org at the one version
pinned in its Directory.Build.props; its Update script moves that pin to the newest release or to any
older one. A release is a vX.Y.Z tag pushed here, which publishes every package at that version: all
other service and Contracts packages first, then CompanyHQ.Rss as the generic completion
marker, then the legacy CompanyHQ.Api host package for companies already pinned to it. Published
versions never change, so a company that does not update keeps building.

From 3.0.0 every package and namespace drops the `Services` segment: `CompanyHQ.Services.Auth` is
now `CompanyHQ.Auth`, `CompanyHQ.Services.Sales.Contracts` is `CompanyHQ.Sales.Contracts`, and the
release feed is `CompanyHQ.Rss`. Releases up to 2.x keep their old names and stay published. A company
on 2.x does not see 3.0.0 from its Update script, which still reads the old feed; to move, change its
package references, `using` directives and the feed address in Scripts/Update.ps1 from
`companyhq.services.rss` to `companyhq.rss`, set the pin to 3.0.0 and start once. Table names are
unchanged, so the migration that start writes contains no schema changes.

C# clients reference the Contracts packages; a company that wants TypeScript types generates them from its own
API. Any company website, including an existing Blazor one, is the company's own and calls its API.
See [the consumer guide](Templates/Company/README.md) for packages, adding modules and the scripts.

Provider boundary upgrades and unavoidable major-version source/binary changes are documented in
[the upgrade guide](Docs/provider-upgrade.md). The bounded two-language architecture proof, including
its repeatable disposable-database verification and real measurements, is in
[Experiments/UniversalApi](Experiments/UniversalApi/README.md); it is not enabled in company hosts.

Verification tooling belongs to HQ, not generated company folders. Run node Scripts/test-companyhq-cli.mjs
only when validating creation changes. HQ CI runs the generic tests, the creation regression in a fresh
disposable directory, and website typechecking against checked-in API types (the website is HQ's own
and is not shipped to companies). Release consumption, shared by the publish workflow and the manually
run "Verify consumer" workflow, creates the starter, pins a published version, restores it from the
feed, builds it, runs it and checks /health and that /api/rss does not exist. Running "Verify consumer"
for an existing version needs no tag and publishes nothing.
