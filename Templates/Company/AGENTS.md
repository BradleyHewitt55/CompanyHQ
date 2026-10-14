# Company project

- The company owns its API, apps, configuration, and scripts; generic service changes belong upstream.
- This starter is a plain ASP.NET API that references only the generic packages it uses (CompanyHQ.Rss
  and its Contracts to begin with). There is no HQ host, `CompanyHQ.Api`, demo, test project, website or assets.
- Add a generic module by referencing its package and Contracts package at the pinned version and
  registering it in `Apps/Api/Program.cs`. The company owns that registration and everything the module
  needs from its host (database, migrations, authentication, providers, configuration).
- Generic packages come from one version pinned in `Directory.Build.props`; there is no HQ source checkout.
- Keep lifecycle automation minimal. Operational details belong in executable files; script-management rules belong in Scripts/AGENTS.md.
- Preserve persistent data and credentials. Do not commit environment secrets or test output.
- Configuration is environment-only: `.env.dev` (development) and `.env` (production). Do not add local state folders.
- Run only the minimum checks relevant to a change, using isolated backing services.
