# Agents

- Only run the minimum number of tests necessary
- Keep `AGENTS.md` focused on project or service rules, decisions, and non-obvious constraints.
  Agents can read the code to understand the implementation; do not duplicate source layouts,
  signatures, endpoint inventories, DTO fields, or configuration references.
- Use `AGENTS.md` directly; do not create separate assistant-specific instruction files.
- Every service project is published as a NuGet package named after its assembly, and `Apps/Api` as `CompanyHQ.Api`, all on the one version in `Directory.Build.props`. Company projects consume those packages, so a public type or registration method is a contract; raise the version to release a change.
