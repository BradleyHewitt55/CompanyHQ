# Payload CMS pattern study

> **Document status:** Reference finding from `.repos/payload`; not CompanyHQ implementation. Inspected during repository discovery.

Payload is a TypeScript monorepo whose core strength is compiling a developer configuration into validated runtime behavior shared by server operations and a generated admin application. Collections, globals, fields, access functions, hooks, endpoints, database adapters, storage adapters, admin components, and plugins enter through configuration; runtime operations centralize authorization, validation, hooks, transactions, versions, and response shaping.

Reusable patterns are a compiled configuration registry, typed field traversal, declarative admin metadata, operation-level pipelines, adapter boundaries, server-generated admin manifests, hook context, and immutable version records. The target equivalent is defined in [CMS configuration](../cms/configuration.md) and [admin routing](../cms/admin-routing.md).

Do not copy Payload internals or assume its multi-tenant plugin is an isolation boundary. In particular, privileged Local API behavior can bypass access when callers request it; CompanyHQ provides no general authorization bypass. CompanyHQ installations are physically independent, and every internal caller carries an actor and policy context.

License compatibility must be checked at the dependency/code level before adopting any package. Architectural ideas are reimplemented behind CompanyHQ-owned contracts.
