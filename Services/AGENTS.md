# Services

This file defines the shared service template and management rules. Read the service's own
`Services/{Name}/AGENTS.md` before changing it. Service-specific rules, decisions, exceptions,
limitations, and wiring belong there; do not add them to this file.

## The template

Services are C# class libraries started by the API host. Each service owns its business rules,
models, contracts, and HTTP surface; hosts register services and consumers call them.

```text
Services/{Name}/
  {Name}.csproj              AssemblyName + RootNamespace = CompanyHQ.Services.{Plural}
  {Name}.http                Requests for every endpoint, including failure cases
  AGENTS.md                  This service's rules, decisions, and wiring
  Contracts/
    {Name}.Contracts.csproj  Client-shippable assembly
    Models/                  Entities and options compiled into the service assembly
    Dtos/                    Response shapes and typed results
    Forms/                   Validated request bodies
    Enums/                   One enum per file
    Ports/                   Narrow integration interfaces, when needed
    OpenApi/{service}.json   Generated HTTP contract, when endpoints exist
  Data/                      Entity configurations: indexes, constraints, column types
  Services/                  I{Name}Service and implementation; all business rules
  Controllers/               HTTP binding and mapping results to status codes
  Extensions/                Add{Plural}(), endpoint mapping, and service helpers
```

Use a singular folder name. When an entity has that same name, use a plural assembly and namespace
to avoid a type/namespace collision; otherwise keep them singular. Contract subfolders have their
own namespace suffix. Omit folders and HTTP files that the service does not need.

## Contracts

- Entities stay inside the process. Never accept or return one from a controller, even nested in a DTO.
- Keep the Contracts assembly free of runtime infrastructure: no framework reference, database,
  cache, or provider dependencies. Its only permitted project dependencies are other Contracts
  assemblies within the dependency rules below. Compile entities, options with infrastructure
  dependencies, entity projections, and provider response shapes into the service assembly instead;
  explicitly re-include those files in the service project.
- Name entity response types `{Role}{Class}Dto`. Put role variants in the same file, named after
  the class without a role prefix or `Dto` suffix. Keep unrelated classes in separate files.
- Start with the narrowest response and derive wider shapes only when they add fields the caller
  may see. Contact details, audit stamps, and relationships can require different views. Do not
  invent empty variants for self-service responses, external lookups, or identical views; document
  the exception in the service's own `AGENTS.md`. Password hashes never appear in DTOs.
- Request bodies belong in `Forms/` and end in `Request`. Put record validation attributes on
  constructor parameters, never on generated properties with `[property: ...]`.
- Services return values or typed errors, never `ActionResult`. Controllers map those to HTTP.

Every HTTP service publishes `Contracts/OpenApi/{service}.json` containing its routes and all
client-facing role shapes, never entities or internal shapes. Controllers and C# contracts are the
source of truth: run `npm run api:document` to generate documents, never hand-edit them.

Consumers never hand-write copies of DTOs. C# clients reference Contracts projects; TypeScript
clients run `npm run api:types`. Schema names match DTO names, enums serialize as strings, and
output must be deterministic for `api:check`. The host discovers documents by controller assembly
and service folder; there is no manual document registration.

## Registration and dependencies

- Register through `Extensions/{Name}ServiceCollectionExtensions.cs` with `Add{Plural}()`.
  Register the service interface and implementation, and mount its controllers with
  `AddControllers().AddApplicationPart(...)`. Controllers live in the service, never the API host.
  Other endpoints use an `IEndpointRouteBuilder.Map{Plural}()` extension in the same folder.
- The host calls each registration once. Do not add re-entry guards, marker types, or `TryAdd`
  fallback adapters. Required constructor dependencies must fail host validation at startup when
  missing. Shared host concerns such as `TimeProvider` and the default 429 status register once.
- Bind configuration with `configuration.GetSection(XOptions.SectionName).Get<XOptions>() ?? new()`
  and validate before registration. Never read environment variables directly; the standard provider
  maps `Section__Key` onto the same section used by appsettings and deployment configuration.
- Register a controller's rate-limit policy in its owning service. Never set shared `OnRejected`;
  use `IRateLimiterPolicy<T>` when a policy needs its own response body.
- Feature services never reference other feature implementations or Contracts. Shared foundation
  dependencies and their usage are documented by the owning foundation services. A provider adapter
  may reference the Contracts assembly for the single port it implements, and no other feature.
- For cross-feature behavior, declare a narrow port in the consuming service's `Contracts/Ports/`.
  Bridges live in `Apps/Api/Integrations/`, translate between services, and hold no business rules.
  Use an optional port only when the service works without it; otherwise require it at startup.
  A service exposing an integration port follows the same rule for bridges supplied to it.
- Keep the dependency graph acyclic, including factory registrations and callbacks. Document
  integration constraints in the affected service's `AGENTS.md`.
- An out-of-process service uses the same registration surface to register a typed client behind
  its interface. Keep the process boundary inside the service.

## Service behavior

- Enforce every business and access rule in the service, including for in-process callers.
  Pass the acting user's id explicitly when a rule depends on it; controllers only bind HTTP input.
- Restricted actions use named permissions, never role-name checks. Enforce them at the controller
  boundary and again in the service. Self-service endpoints authenticate the caller and check
  ownership. Resolve wider response audiences explicitly; default to the narrowest view.
- Filter, sort, search, and page in the data store before materializing. List endpoints use
  `[FromQuery] PageRequest paging` and `[FromQuery] SearchRequest search` with the caller's DTO
  shape, rather than inventing separate search parameters. Storage-specific mechanics belong in
  the storage service's documentation; non-database exceptions belong in their owning service.
- An owned entity normally gets list, get, create, update, and delete operations. Document any
  rule that forbids an operation in the service's own `AGENTS.md`.
- Delete is a hard delete unless the service documents a retention rule. Use cascading foreign
  keys for dependent rows and ports for cleanup owned by another service.
- Exercise behavior against real infrastructure; do not introduce mock data.

## Creating and changing a service

A new service is complete only when it includes its own `AGENTS.md` in the same change as the code.
For HTTP services, include `{Name}.http` requests for every endpoint and its failure cases, then
regenerate the contract and consumer types.

Keep the service's `AGENTS.md` focused on four sections:

1. What it owns and does not own (at most three sentences).
2. Rules a change must not break.
3. Decisions and what was deliberately not built.
4. Non-obvious traps, integration constraints, and known limitations.

Document service rules, decisions, and the reasons behind them. Agents can read the code to
understand its implementation: do not narrate the source or repeat the shared folder layout,
method signatures, endpoint inventories, DTO field lists, configuration tables, or client page
descriptions. Include integration details only when they explain a constraint or a non-obvious trap.
Do not create a mirrored `Docs/Services/{Name}.md` reference.

When changing a service, apply its documented rules and update its own `AGENTS.md` in the same
change whenever behavior, rules, or decisions change. If code conflicts with that document, the
document wins until explicitly updated with the code. Put service-specific exceptions and new
decisions in that service's file, never here. Update this file only when the shared template or
service management conventions change for all services.
