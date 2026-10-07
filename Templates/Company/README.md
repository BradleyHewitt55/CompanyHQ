# Company website

From this project folder:

```powershell
.\Scripts\StartDev.ps1         # default --Web: Next.js on host, API and infrastructure in Docker
.\Scripts\StartProd.ps1        # build and start the entire stack in Docker
.\Scripts\Update.ps1           # pull HQ updates; rebuild afterward
```

The public home page, sign-in pages and admin pages belong to one Next.js app. StartDev accepts
--{project} for a configured Node web app. The three scripts are self-contained; there is no consumer
CI, test runner, helper script or bundled CLI. Startup creates only ignored runtime state under .local,
not an artifacts directory. Docker volumes and local storage survive restarts.

## Seed an administrator

Copy .env.example to .env for production or .env.dev for development, then replace its email and
password. Development reads .env followed by .env.dev; process environment variables take precedence.
The supported values are Seed__Admin__Email, Seed__Admin__Password, Seed__Admin__FirstName and
Seed__Admin__LastName. Both email and password are required. Simple literal dotenv values, optionally
wrapped in matching quotes, are supported; shell expansion and multiline values are not.

On API startup the existing HQ seeder creates a missing verified administrator with a hashed password.
It preserves an existing administrator's password and refuses to promote an existing non-admin account.
Changing the environment password does not reset an existing account: use its password reset flow.
Credentials are private and must never be committed. Existing demo credentials remain usable unless
explicitly configured differently. First company-profile initialization needs valid seeded credentials.

## Use your existing furniture website, including Blazor

Keep the existing website and its UI. There is no need to replace it with this Next.js starter, move it
into CompanyHQ, or run the starter website. Add the API as a backing service and connect only the
features your site needs. The create command requires an empty destination; do not run it over an
existing website. The current startup scripts launch a Node website, so an existing Blazor website
keeps its own dotnet launch command and deployment, or gets company-specific lifecycle scripts.

Currently generic implementations and contracts come from Git source ProjectReference dependencies,
not published CompanyHQ NuGet packages. The consumer API references HQ's API project, which references
the individual service projects. Updating the HQ checkout and rebuilding compiles the selected source.
Blazor clients reference only the needed Contracts projects; they should not pull server implementations,
EF Core, provider credentials or database access into a browser build.

For example, if an existing website project sits at Apps/Web, a managed dependency reference is:

```xml
<ItemGroup>
  <ProjectReference Include="../../Services/YourService/Contracts/YourService.Contracts.csproj" />
</ItemGroup>
```

Company-owned services reference local contracts; generic services reference the selected HQ checkout.
Adjust the path for your existing project location. These contracts currently target .NET 10, so an
older Blazor project needs an explicit framework compatibility decision before referencing them.

Use a typed HttpClient with the relevant C# Contracts assembly to call your API.

For Blazor Server, calls run on the server. Keep the current UI and acquire/forward each user's token
through your authentication integration; the site's own login cookie does not automatically authenticate
with this JWT API. For WebAssembly, calls run in the browser; register the client in the browser project,
use the public API address and allow its exact origin, Authorization header and required verbs in API CORS.
The existing marketing CORS policy allows only public calls and is insufficient for authenticated to-do
PUT/DELETE requests. For Auto/prerendered components, registration may be needed on both server and client.
See [Microsoft's Blazor API guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/call-web-api?view=aspnetcore-10.0).

The TypeScript version uses npm run api:types to generate src/api/schema.d.ts from HQ's published service
OpenAPI documents. These declarations give compile-time shapes; HTTP JSON carries actual values.
API changes still need corresponding client work if their contracts break. Newly introduced generic services
may also require explicit registration in your company-owned API host; updates do not rewrite that host.

## Choose when to update

A managed project stays on the exact commit recorded in .companyhq/lock.json until Update is run.
Keep and commit that lock file. After a fresh clone, the first update restores the locked revision;
a subsequent update advances to the latest saved branch. Updates preserve company-owned UI and scripts.
The Northstar demo instead links ../CompanyHQ: rebuilding immediately uses that folder's local changes.
For independent old-version retention, use a separate managed checkout for your existing company.

Publish shared HQ code to GitHub before consumers can fetch it. Run Update, review contract/migration
changes, regenerate client types when needed, then rebuild. There is no automatic upgrade at startup and
no dedicated release-picker/rollback command. Database migrations may need separate rollback planning.

Local email and file storage are demo adapters. Choose real providers for deployment. Configure unique
ports and Compose project names before the first start if another project is running.
