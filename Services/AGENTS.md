# Services

How to build one. Each service documents itself in its own `AGENTS.md`; this file is the shape they
all share.

A service owns its models, its rules, its DTOs, and the controller that exposes them. The API host
only registers it and the admin console only calls into it — **no business rule lives in a host.**

**Services are C#.** A service is a C# class library with a C# controller, and the API host is the
only thing that starts it.

A service is still three things:

1. **A model**, persisted in the service's own store, that never leaves the process.
2. **Role-shaped DTOs** (`UserPostDto` / `AdminPostDto`) that are what actually goes on the wire.
3. **`Contracts/OpenApi/{service}.json`**, describing every route and every one of those DTOs.

That JSON document is the contract every consumer reads, and it is what makes the arrangement work:
**no consumer ever hand-writes a DTO.** A C# client may reference the `Contracts` project directly;
a TypeScript UI generates its bindings from the document with `openapi-typescript` and gets the full
set of shapes for free. The document carries the **client-facing** DTOs — the role-shaped ones, for
`Admin` / `Agent` / `User` or whatever roles that service has — and never an entity or a
service-internal shape. A DTO that exists only as a C# type and is not in the document is invisible
to every TypeScript app, and the service is not finished.

**A new service is not finished until it has its own `AGENTS.md`.** Write it in the same commit as
the code. It is the only place the reasoning survives — the next agent reads the doc before it reads
the source, and an undocumented service gets rebuilt wrong. See [Before it is
finished](#before-it-is-finished) for what goes in it.

## The template

```
Services/{Name}/
  {Name}.csproj              AssemblyName + RootNamespace = CompanyHQ.Services.{Plural}
  {Name}.http                Requests that exercise every endpoint
  AGENTS.md                  This service's own reference
  Contracts/
    {Name}.Contracts.csproj  The client-shippable assembly. No dependencies at all.
    Models/                  Entities (: BaseModel) and options — compiled into the service instead
    Dtos/                    What goes out on the wire — one file per class, named for it
    Forms/                   What comes in — request bodies with their validation
    Enums/                   Enums, one per file
  Data/                      IEntityTypeConfiguration<T> — indexes, constraints, column types
  Services/                  I{Name}Service + implementation. Every rule lives here.
  Controllers/               HTTP only. Maps results to status codes and nothing else.
  Extensions/                Add{Plural}() and any queryable/claims helpers
```

Each `Contracts/` subfolder is its own namespace suffix (`…Contracts.Models`, `…Contracts.Dtos`), so
a controller usually needs a `using` per folder. That is deliberate — an import crossing a boundary
should be visible in the file header.

Where a service has a `Contracts/` assembly, it builds as its own assembly and **must stay
dependency-free** — no `FrameworkReference`, no EF Core, no Npgsql, no Cache. A C# client may
reference these projects directly instead of generating from the document, and those clients target
constrained runtimes such as `net10.0-android`, where none of that exists. A contract may reference
another service's `Contracts` project and nothing else. Anything that touches an entity — an
entity-to-DTO projection, a `From(Entity)` factory, an upstream provider's response shape — belongs
in the service assembly, and the service `.csproj` re-includes those files explicitly.

**The folder is singular, the assembly and namespace are plural** when the service owns an entity of
the same name: `Services/Message` ships `CompanyHQ.Services.Messages`. A type named `Message` inside a
namespace named `Message` is ambiguous to resolve, and the fix is not to rename the type. A service with no such
clash stays singular.

Drop `Data/` if the service has no entity, and `Controllers/` if it has no endpoints.

### The document

Every HTTP-exposing service owns `Contracts/OpenApi/{service}.json`. **Nobody hand-edits it and
nobody copies a DTO out of it into an app.** Both sides of it are generated: the document from the
host's controller metadata, the TypeScript bindings from the document.

`npm run api:document` builds `Apps/Api` and reads its controllers, so the C# contracts and
controllers stay the source of truth and the document is a build output of them. Never hand-maintain
both sides.

Consumers pull from the document and only from the document. A TypeScript app selects the service
documents it wants with `npm run api:types` and gets a `schema.d.ts`; a C# client references the
`Contracts` project instead of generating. That is the whole point: adding a service means every
existing UI regenerates and immediately sees its shapes.

Three things make a document usable, and all three are easy to get wrong:

- **Name the schema components exactly as the DTO rule requires:** `UserPostDto`, `AdminPostDto`,
  never a bare `PostDto`. Component names are what every generator turns into type names, so one
  sloppy name in the document reshapes every client at once.
- **Describe enums as strings.** A generator that sees an integer enum emits `0 | 1 | 2` and the
  names are gone. That is `[JsonStringEnumConverter]` in C# and a `"type": "string"` with an `enum`
  list in the schema.
- **Register it in `Scripts/openapi-manifest.mjs`:** an entry in `serviceDocuments` with its
  `routePrefix`, plus the service name in each `typeScriptConsumers` list that should see it.
  Unregistered means no app gets types for it and `npm run api:check` will not guard it. Emit
  stably while you are there: `api:check` re-runs generation and fails on any byte that moved, so
  an emitter with unordered keys or a timestamp in `info` turns every unrelated PR red.

A service that runs out of process still ships an `Extensions/Add{Plural}()` for the API host, but
it registers a typed client over that process rather than a concrete implementation. The host sees
`I{Name}Service` and the generated contract types, so the process boundary never leaks past the
service folder.

## Registration

One extension, named for the service, in `Extensions/{Name}ServiceCollectionExtensions.cs`:

```csharp
public static IServiceCollection AddPosts(this IServiceCollection services)
{
    ArgumentNullException.ThrowIfNull(services);

    services.AddScoped<IPostService, PostService>();
    services.AddControllers().AddApplicationPart(typeof(PostServiceCollectionExtensions).Assembly);

    return services;
}
```

A service ships its **own** controller and mounts it with `AddApplicationPart`. Controllers never
live in `Apps/Api`. Anything else a service exposes — a hub, the MCP endpoint — ships as an
`IEndpointRouteBuilder.Map{Plural}()` in the same `Extensions/` folder, so the host only calls it.

The host calls each `Add{Plural}()` exactly once, so an extension carries **no re-entry guard, no
marker type, and no `TryAdd` "default" for another service to override**. A port with no adapter is
not papered over with a null implementation: the host validates every constructor dependency at
build time in every environment, and a missing adapter stops startup naming the type.
`TimeProvider` and the `429` rejection status are registered once by the host; a service never
registers its own copy.

Configuration binds by section and nothing else:
`configuration.GetSection(XOptions.SectionName).Get<XOptions>() ?? new()`, then `Validate()` before
registering. Never read `Environment.GetEnvironmentVariable` — the environment reaches a section as
`Section__Key` (`Postgres__Password`, `Cloudflare__R2__Bucket`) through the standard provider, so
`appsettings.json`, `.env`, and compose all name a setting the same way.

A rate-limit policy belongs to the service whose controller names it:
`services.AddRateLimiter(o => o.AddPolicy(...))` inside `Add{Plural}()`. Never set `OnRejected` on the
shared options; a policy that needs its own rejection body implements `IRateLimiterPolicy<T>` (see
Geo).

A persisted model needs no registration. Postgres discovers models from every project in the host's
dependency graph that references it, for both the running host and `dotnet ef`.

`AddPostgres` registers first, then `AddUsers`, then everything else.

## Depending on another service

**A feature service never references another feature service** — not its implementation and not its
`Contracts` project. Every service is a plug-in a company can take or leave, so none may assume
another is there. The only references a service may hold:

- **Foundation:** `Postgres`, `User` (the account and `User.Audience()`), `Auth/Contracts` (claims
  and policies), and `Cache`.
- **A provider adapter's one port:** `Resend`, `Cloudflare` and `Firebase` exist to fill exactly one
  port of `Email`, `Media` and `Notification`, and reference that service's `Contracts` for it.
  They reference nothing else.

When a service needs something another one does, it declares a narrow port in its own
`Contracts/Ports/`, in its own words (`IMessageNotifier`, `IContactMailer`), and takes it as a
constructor dependency. The bridge between the two lives in **`Apps/Api/Integrations/`** and nowhere
else: a small adapter that implements one service's port by calling the other's. Adapters translate;
they hold no business rule.

Take a port as **optional** (`IMessageNotifier? notifier = null`) when the service still makes sense
without it, and required when it does not (`Contact` cannot route without `IContactMailer`). A
required port with no bridge stops startup, naming the type.

The same holds in reverse. A service that other services can plug into (`Moderation` reports,
`Media` read policies) publishes its port in `Contracts/Ports/`, and the bridge that feeds it lives in
the host, never in the feature being plugged in.

## What gets a DTO

**An entity never crosses the wire.** Not as a return type, not nested inside one. If a controller
signature mentions a `BaseModel` subclass, that is a bug.

| Shape | Goes in | Rule |
| --- | --- | --- |
| Entity | `Contracts/Models/` | Inherits `BaseModel`. Never returned, never accepted. |
| Response | `Contracts/Dtos/` | Type named `{Role}{Class}Dto`; **file** named after the class — no role prefix, no `Dto` suffix (`Post.cs`). Role variants share that file, narrowest first. Unrelated classes never do. |
| Request body | `Contracts/Forms/` | `…Request`. Validation on the **constructor parameters**. |
| Result-or-error | `Contracts/Dtos/` | A `Result<T>`-style struct. Never an `ActionResult` from a service. |

Validation attributes go on record **constructor parameters**, not the generated properties. MVC
throws at request time for a record whose validation metadata is on the properties — do not add a
`[property: …]` target.

```csharp
public sealed record CreatePostRequest(
    [Required, MinLength(1), MaxLength(50_000)] string Description,
    IReadOnlyList<Guid>? TaggedUserIds = null);
```

## Client-facing role shapes

A service that returns an entity ships **one DTO per role that has a different view of it**, named
`{Role}{Class}Dto` — `UserPostDto` / `AdminPostDto`, never a bare `PostDto`. Start with `User` (the
narrowest) and let each more privileged role derive from it. One endpoint serves all of them; a
higher role gets the same JSON with extra properties, and the narrower shapes are what every role
is also allowed to see. All of these client-facing DTOs are what goes into
`Contracts/OpenApi/{service}.json`.

The mechanics — why `ResponseAudience.Client` is `0`, why the audience is a parameter and never
ambient state, and how the projection upcast keeps EF Core in SQL — are in
[`Postgres/AGENTS.md`](Postgres/AGENTS.md#response-audiences). Read that before adding a DTO.

### When to split

Split when the entity has a field a caller in a given role has no standing to see. In practice that
is:

- **Contact details** — an address is a harvesting target in a list endpoint.
- **Audit stamps** — `UpdatedAt` is operator information; a reader needs `CreatedAt`.
- **Relationships** — who else is attached to a row, when the caller is not one of them.

### When not to

- **The DTO describes the caller to themselves.** Your own session or profile has no privileged
  view — an "admin version" of it is meaningless.
- **The service owns no entity.** A lookup result from an external API has no stored row to have a
  privileged view of.
- **There is nothing operational to add.** Do not pad a role DTO to satisfy symmetry. If the model
  genuinely has one role's field and nothing extra for the others, say so in the service's
  `AGENTS.md` and leave the split out until it earns one.

A role DTO that adds nothing is worse than no role DTO: it implies a boundary that isn't there.

**A password hash never appears in any DTO.** Map with EF `Expression`s so it is never even selected
out of Postgres.

## Services and controllers

- The service enforces every rule **itself** and takes the acting user's id when a rule depends on it.
  A controller must never decide a caller is allowed to do something.
- A service returns a value or a typed error. Mapping an error to a status code is the controller's
  job — that is the only reason a result struct exists.
- Reach for `IPostgresService<T>` by default. Inject `PostgresDbContext` only when a write spans two
  tables, because two repository calls are two transactions.
- Filter, sort, and page **in SQL**. `ToPagedResultAsync` with a projection; never materialise a list
  and `Where` it.
- No mock data. Exercise against the real `postgis/postgis:17-3.5` container.

## Before it is finished

- **`Services/{Name}/AGENTS.md` — mandatory, and the only service doc.** Four sections only:
  what it owns and does not own (3 sentences max), rules a change must not break,
  decisions taken with what was deliberately *not* built, traps and wiring. Anything
  non-obvious: gotchas, known limitations.

  **Do not narrate the source tree.** No file-by-file layout table, no method-by-method inventory, no
  restating a signature the reader can hit F12 on. The folder layout is in [the template](#the-template)
  and identical everywhere; repeating it per service is noise that goes stale. Document what the code
  cannot tell you: why it is this way, and what breaks if you change it.
- `{Name}.http` — a request per endpoint, including the failure cases.

  Do not create a `Docs/Services/{Name}.md` mirror. Endpoint tables, registration snippets,
  DTO field lists, and client page descriptions rot first; controllers, `{Name}.http`,
  and the template above are the truth for those.

Changing a service means changing its `AGENTS.md` in the same edit. If code conflicts with
`AGENTS.md`, the doc wins until it is updated explicitly in the same PR. A doc that has drifted
is worse than no doc, because it is trusted.

> On first create, add a CLAUDE.md with a @AGENTS.md as the ref
