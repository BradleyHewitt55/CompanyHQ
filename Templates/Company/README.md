# Company API

A plain ASP.NET API that uses the CompanyHQ packages directly. It starts with the generic RSS
service as a small working example: `CompanyHQ.Services.Rss` and `CompanyHQ.Services.Rss.Contracts`,
registered with `AddRss` for in-process use. The API maps `/health` (and OpenAPI in Development) and
exposes no RSS route; a controller or service you write injects `IRssService` when it needs a feed.

From this project folder:

```powershell
.\Scripts\StartDev.ps1         # API in Docker, then returns (add --Web to run your own Apps/Web on the host)
.\Scripts\StartProd.ps1        # build and start everything in docker-compose.yml
.\Scripts\Update.ps1           # choose the CompanyHQ release; rebuild afterward
```

Docker is required; the .NET SDK is only needed to build outside Docker. Configuration comes from
`.env.dev` (StartDev) or `.env` (StartProd); `.env` is created with the same keys, all empty. The
CompanyHQ packages are public on nuget.org, so no token or sign-in is needed, locally or in CI.
Ports and the Compose project name come from `project.json`. Development and production are separate
stacks; starting one stops the other because they publish the same port.

## CompanyHQ packages

Generic implementations and contracts come from public NuGet packages on nuget.org, pinned to the one
release in `Directory.Build.props`; there is no HQ source checkout and no `CompanyHQ.Api` host package.
Creation pins the newest release present in the package feed (read through the Rss package) and fails
when none is published. `Update` with no argument moves to the newest release, `Update 1.0.0` pins a
specific one and `Update --list` shows what is published. It changes only the pin, never your code.
Published releases never change, so a company that stays on an older one keeps building.

## Adding modules

Each module is a package plus its Contracts package, both at the pinned version. To add one:
reference both in `Apps/Api/Company.Api.csproj` as the Rss ones are (`Version="[$(CompanyHQVersion)]"`),
then call its `Add…` registration in `Apps/Api/Program.cs`. This API owns everything a module needs from
its host: the database and migrations, authentication, provider adapters, configuration and any routes.
Read the module's own documentation in the CompanyHQ repository before adding it, and read the release
notes before moving the pin (see `Docs/provider-upgrade.md` there for the last breaking change).

C# clients reference a module's Contracts package. This starter generates no OpenAPI files or website
types; add that to your API when your own client needs it.

## Website and tests

The starter has no website and no test project. A web app is yours to add: put it in `Apps/{Name}`,
list its port under `apps` in `project.json`, run it on the host with `StartDev.ps1 --{Name}`, and add
its service to `docker-compose.yml` for production. Add tests to a project of your own and a step for
them to `.github/workflows/company-ci.yml`, which only builds the API until you do.
