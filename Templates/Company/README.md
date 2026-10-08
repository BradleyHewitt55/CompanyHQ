# Company website

From this project folder:

```powershell
.\Scripts\StartDev.ps1         # default --Web: Next.js on host, API and infrastructure in Docker
.\Scripts\StartProd.ps1        # build and start the entire stack in Docker
.\Scripts\Update.ps1           # choose the CompanyHQ release; rebuild afterward
```

The public home page, sign-in pages and admin pages belong to one Next.js app. StartDev accepts
--{project} for a configured Node web app and keeps it in the foreground; Ctrl+C stops it. The three
scripts are self-contained and create no local state: configuration comes from .env.dev (StartDev)
or .env (StartProd), and the database and uploaded media live in Docker volumes that survive restarts.
Each mode has its own database, so development data never reaches production; starting one mode
stops the other because they publish the same ports.

Docker, Node and the .NET SDK must be installed.

## Configure and seed an administrator

Fill in every value of .env.dev before the first development start, and of .env before the first
production start; .env is created with the same keys, all empty. Compose hands the mode's file to
the database and the API, so they share one set of credentials. The seed
values are Seed__Admin__Email, Seed__Admin__Password, Seed__Admin__FirstName and Seed__Admin__LastName.
Both email and password are required. Use simple literal values; shell expansion and multiline values
are not supported. Ports and the Compose project name come from project.json.

On API startup the HQ seeder creates a missing verified administrator with a hashed password. It
preserves an existing administrator's password and refuses to promote an existing non-admin account.
Changing the environment password does not reset an existing account: use its password reset flow.
Credentials are private and must never be committed.

## CompanyHQ packages

Generic implementations and contracts come from CompanyHQ NuGet packages on GitHub Packages; there is
no HQ source checkout. The company API references CompanyHQ.Api, which brings every generic service; a
company service references only the packages it uses. Downloading them needs a GitHub token with
read:packages: sign in to the GitHub CLI (gh auth refresh -s read:packages), or set
COMPANYHQ_PACKAGES_TOKEN in the mode's env file. CI needs it as a repository secret.

The company builds against the one CompanyHQ version in Directory.Build.props until Update is run.
Update with no argument moves to the newest published release, Update 0.1.0 pins a specific one
(older or newer) and Update --list shows what is published; it changes nothing else. Published
releases never change, so a company that stays on an older one keeps building. There is no automatic
upgrade at startup.

## Database migrations

This company owns its migrations (Apps/Api/Migrations), covering the generic tables and its own.
StartDev and StartProd write them: when a company model changed, or a CompanyHQ update changed a
generic one, the script adds a migration before building, and the API applies it when it starts. The
first start writes the Initial migration. Commit every migration with the change that needed it.

A migration that would delete data, such as a removed column, is written but not applied: the script
stops so it can be reviewed, and running the script again applies it. Rolling a database back is a
separate decision; the scripts only move forward.

## Company services, contracts and tests

Add a company service under Services: reference the CompanyHQ packages it uses, reference the service
from the company API, and register it there. A CompanyHQ update never replaces company services.

Building the API image writes one OpenAPI contract per company service. StartDev and StartProd copy
each into Services/{Name}/Contracts/OpenApi/{name}.json; commit them with the C# contracts. npm run
api:types (in Apps/Web) does the same and also generates src/api/schema.d.ts from the document
covering every generic and company controller, so TypeScript types are never hand-copied.

A Blazor or other .NET client needs no generation step: it references the C# contracts directly, the
CompanyHQ.Services.{Name}.Contracts packages for generic DTOs and the company's own Contracts projects
for its services. Contracts carry no database or provider code, so they are safe in a browser build.

Company tests live in Tests as one xUnit project, the same flow CompanyHQ uses: dotnet test
Tests/Company.Tests.csproj boots the company API in memory against disposable Postgres and Redis
containers and never touches the development database. Company CI (.github/workflows/company-ci.yml)
runs those tests and typechecks the website.

Local email and file storage are demo adapters. Choose real providers for deployment.
