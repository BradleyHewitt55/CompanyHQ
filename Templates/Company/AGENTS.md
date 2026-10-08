# Company project

- The company owns its apps, assets, configuration, and scripts; generic service changes belong upstream.
- Keep lifecycle automation minimal. Operational details belong in executable files; script-management rules belong in Scripts/AGENTS.md.
- Preserve persistent data and credentials. Do not commit environment secrets or test output.
- Configuration is environment-only: secrets and the admin seed live in `.env.dev` (development) and `.env` (production), persistent data in Docker volumes. Do not add local state folders.
- Run only the minimum checks relevant to a change, using isolated backing services.
- Generic services come from CompanyHQ NuGet packages pinned to one version in `Directory.Build.props`; there is no HQ source checkout.
- This company owns its migrations for the whole schema. The start scripts write them; never hand-edit the snapshot, and commit each migration with the change that needed it.
