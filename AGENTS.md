# Agents

- Only run the minimum number of tests necessary
- Keep `AGENTS.md` focused on project or service rules, decisions, and non-obvious constraints.
  Agents can read the code to understand the implementation; do not duplicate source layouts,
  signatures, endpoint inventories, DTO fields, or configuration references.
- Use `AGENTS.md` directly; do not create separate assistant-specific instruction files.
- Every service project is published as a NuGet package named after its assembly, and `Apps/Api` as `CompanyHQ.Api`. Company projects pin one release and consume only those packages, so a public type or registration method is a contract.
- A release is a `vX.Y.Z` tag; the tag is the version of every package. Never move or reuse a tag: companies stay on older releases, so a published version must never change.
