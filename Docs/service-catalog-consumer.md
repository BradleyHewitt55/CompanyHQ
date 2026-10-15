# Runtime service catalog consumer handoff

The runtime catalog ships in 3.1.1; the v3.1.0 tag failed its tests and published no packages. Every package in a
company remains pinned to one exact published tag version; do not change, move or republish 3.0.0.
New companies reference the generic implementation and Contracts packages they use directly,
including CompanyHQ.ServiceCatalog and CompanyHQ.ServiceCatalog.Contracts. They do not inherit
CompanyHQ.Api, HQ assets, website or test projects. Complete releases are discovered through
CompanyHQ.Rss after its sibling packages publish.

## Host registration

Register the infrastructure, every permitted feature and its required integration ports once,
regardless of persisted enablement. Keep ValidateOnBuild enabled. Discover the full model graph and
apply company-owned migrations before HTTP/worker operations; toggles never remove tables or DI.
The HQ reference wiring is Apps/Api/Program.cs and Apps/Api/Integrations/HqServiceCatalog.cs; a
consumer implements these responsibilities in its own host and bridges, rather than inheriting HQ.

After AddCompany(), call AddCompanyServiceManagement() to register persisted selection reading,
management and safe navigation. This is explicit opt-in: a legacy profile-only registration does not
expose management controllers with unresolved dependencies. Catalog controls do not require a
CompanyProfile. GET /api/company remains anonymous 404 until the real profile is saved.

Each feature supplies an additive registration in its existing Extensions namespace:

```csharp
services.AddAppointmentsServiceDescriptor(); // appointments
services.AddAuthServiceDescriptor();         // auth
services.AddBlogsServiceDescriptor();        // blogs
services.AddCompanyServiceDescriptor();      // company
services.AddContactServiceDescriptor();      // contact
services.AddEmailServiceDescriptor(loadedProvider: "resend");
services.AddFaqsServiceDescriptor();         // faqs
services.AddGeoServiceDescriptor(loadedProvider: "openstreetmap");
services.AddGitHubServiceDescriptor();       // github
services.AddInboxServiceDescriptor();        // inbox
services.AddMcpServiceDescriptor();          // mcp
services.AddMediaServiceDescriptor(loadedProvider: "s3");
services.AddMessagesServiceDescriptor();     // messages
services.AddModerationServiceDescriptor();   // moderation
services.AddNotificationsServiceDescriptor(); // notifications
services.AddRssServiceDescriptor();          // rss
services.AddSalesServiceDescriptor();        // sales
services.AddShippingServiceDescriptor();     // shipping
services.AddUsersServiceDescriptor();        // users
services.AddWidgetServiceDescriptor();       // widget
```

Use only the descriptors/services the company actually permits. Descriptor methods accept an
optional isConfigured callback for actual loaded provider readiness. Use options or neutral
infrastructure, not recursively admitted feature operations. Custom transports registered through
factories/instances must supply their actual readiness callback. Widget's optional
isCapabilityConfigured callback must verify each destination independently, offline; its default
conversation capability requires a registered port and ready Inbox, and issue readiness needs the
explicit host callback. HQ checks a registered issue port and the stored repository connection.
Sales/Shipping capabilities also check that the host registered their narrow ports.

AddMcp(configuration) follows feature descriptors and MVC application parts so additive tool
ownership is known. AddServiceCatalog(params string[] allowedServices) is last, after all features,
ports and MCP are registered. The allowlist names canonical descriptor keys explicitly; a referenced
assembly does not automatically become exposed. Required dependencies must be allowlisted and
acyclic. Capability dependencies can be reciprocal, such as Sales quoting Shipping and Shipping
reading the Sales catalog; capability evaluation never creates a DI or required-dependency cycle.

Company pages own their destinations. Override metadata before AddServiceCatalog when needed:

```csharp
services.ConfigureServiceNavigation("sales", "/admin/sales", "sales.manage");
services.ConfigureServiceNavigation("blogs", "/admin/blog", "blogs.manage", "list_blog_posts");
```

AddServiceConfigurationKeys(key, params ServiceConfigurationKey[]) adds company-owned flags to a
registered descriptor without storing values. Common keys stay visible independently of provider
choice; provider-specific keys come from only the selected provider in company settings UI.

A personal service uses AddServiceDescriptor(new ServiceRegistration(new ServiceDescriptor(...,
Owner: "company", DefaultEnabled: true, MenuPath: "/admin/company-feature",
ReadPermission: "the-company-permission"), typeof(PersonalService).Assembly)). Its controller/port
assembly must be owned by that descriptor; use explicit ServiceOwnerAttribute metadata for endpoints
sharing an assembly. Company-owned authorization must understand its own permission names.
Register a personal descriptor and include its key in the same explicit allowlist.

```csharp
app.UseRouting();
app.UseServiceCatalog(); // before authentication, authorization and endpoint execution
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapMessages();
app.MapMcpEndpoint();
```

MVC ownership follows the registered controller assembly. Minimal/custom endpoints need
WithMetadata(new ServiceOwnerAttribute("canonicalkey")); generic Message and MCP endpoint mappings
already attach ownership. Domain interfaces and source integration ports are admitted through the
opt-in registration wrapper. Public concrete domain services also have availability-aware constructors
for their operation gates. Existing strict constructors remain available; DI selects the longer
constructor when availability is registered. Manually constructing the old standalone constructor
retains standalone behavior and must not be used to bypass a catalog host.

## Public HTTP and Contracts

CompanyHQ.Company.Contracts carries ChangeServiceRequest, ServiceChangeResult, ServiceChangeError
and the ICompanyServiceNavigation port. CompanyHQ.ServiceCatalog.Contracts carries ServiceDescriptor,
provider/configuration/capability metadata, ServiceCatalogDto, ServiceNavigationDto, ServiceStatus,
ServiceSelection, IServiceAvailability, IServiceSelectionReader and typed unavailability exceptions.
Metadata contains keys and protections, never values, API keys, tokens, encrypted credentials or
secret-file contents. Domain configuration metadata and provider configuration metadata are separate.

- GET /api/company/services: full allowlisted catalog; company.services.manage required.
- GET /api/company/services/{key}: full description/details/state; unknown or unallowlisted key is 404.
- PUT /api/company/services/{key}: { "enabled": true/false, "selectedProvider": "provider-key" };
  management permission required at HTTP and again in-process. Enabled is the full requested state;
  an omitted/null provider retains the current/default selection. Success is 200 ServiceChangeResult.
  Unknown key is 404; invalid input is 400; protected core, unmet dependencies, unknown provider,
  unsafe storage changes or a concurrent selection conflict return 409 with the typed error.
- GET /api/company/navigation: authenticated, no ManageServices requirement. Returns only
  ServiceNavigationDto { key, name, destination, status } for enabled destinations whose named read
  permission (or explicit alternative) the account's current persisted role grants. It contains no
  descriptor, provider or configuration metadata. Unauthenticated requests are 401.

ICompanyServiceCatalog in the Company implementation supplies ListAsync, GetAsync and ChangeAsync
with an explicit acting user id. ICompanyServiceNavigation.ListAsync takes the acting user id and
reads the current role, including for in-process callers holding an already-resolved port. The full
management page/switches use the management API, while ordinary staff navigation uses the safe
endpoint. Do not fetch management as a prerequisite for staff navigation and do not use AdminSeed,
legacy role labels or old token permission claims as current authority. Dashboard/account defaults
and their URL prefix remain company-owned. Users appears only for users.manage; companies may merge
that safe entry with their own core Users destination.

sales.manage and blogs.manage alone work for their respective navigation entries. blogs.manage is an
additive HTTP/domain authoring and draft-read aggregate; the existing individual Blog action grants
remain supported. MCP still requires the individual action grants, current owner/assigned-role
intersection, read-only/write restrictions and both deny lists. The aggregate does not grant MCP
capabilities implicitly. company.environment.manage is defined for the separate company-owned
actual-environment-file editor; this change adds no secret override store or environment edit API.

Use generated Company OpenAPI and Apps/Web/src/api/schema.d.ts for HTTP consumers. Do not hand-copy
these DTOs. ServiceCatalog itself has no HTTP document; its nested shapes are in Company OpenAPI.

## Enablement, readiness and provider selection

Status strings are Disabled, EnabledNotConfigured and EnabledReady. Enabled selects feature admission;
readiness describes loaded prerequisites. Enabled but unconfigured services can retain their menu
entry for authorized configuration work. Disabled entries disappear from safe navigation. Auth,
Users, Company, Email and Media are protected enabled; RSS is also protected so release discovery
never depends on optional switches. Contact requires Company/Email; Messages requires Moderation.
Disabling a required target while a dependent remains enabled is rejected, and enabling a dependent
whose required target is disabled is rejected.

Every operation admission reads committed selection state through a fresh scope, not a startup cache
or the request's tracked/uncommitted entity. Database toggles affect the next HTTP, MCP, domain,
provider or worker admission without restart. Disabled owning features return HTTP 404 before
authentication; enabled operations requiring unconfigured providers return 503. An enabled bridge
requiring a disabled/unconfigured optional target raises ServiceDependencyUnavailableException and
returns 503, not fabricated empty products, address results or successful undiscounted quotes.
MCP removes disabled owned tools from tools/list and rejects direct tools/call with a protocol error;
disabling Mcp also makes transport, credential/OAuth controllers and metadata return 404. New SignalR
admission is gated. Already established connections, accepted SDK work and issued signed URLs cannot
be recalled by a switch.

Provider selection is persisted separately from the public profile and never deletes credentials.
A selected provider different from the loaded one yields EnabledNotConfigured, RestartRequired=true
and pauses provider-dependent workers until company wiring/configuration matches after restart.
Show only the selected provider's settings in company pages, retain the former provider's actual
credential files, and use configuration-key Secret/ReadOnly/ProtectionReason/RestartRequired metadata.
Environment changes are normally restart-required because SDK/options are loaded process state.

HQ uses ServiceProviders:Email (resend or localsmtp, default resend) and ServiceProviders:Media
(s3 or local, default s3) to identify loaded transport choice; environment separators are __.
HQ implements the actual Resend and R2/S3 transports. Local SMTP and local storage are company-owned
provider ports, not new generic SDKs or simulated transports. Missing R2 values/bucket register
explicit unavailable storage; missing Resend key/sender registers explicit unavailable email.
ServiceCatalog:LoadFirebase defaults true; false ignores inactive Firebase credentials and exposes
unconfigured push. Fully supplied active Firebase credentials are validated eagerly; malformed
active GitHub encryption configuration still rejects startup. No third-party-login or SendGrid SDK
was added. OpenStreetMap belongs under Geo, Resend under Email, Firebase under Notifications and
S3 under Media; Cache, Persistence and Postgres are infrastructure, not catalog features.

Media sets ProviderChangeRequiresMigration and management rejects a different storage selection
with UnsafeProviderChange. Existing rows have no provider affinity. Do not relax this protection
without migrating bytes/references or introducing explicit affinity; a fresh company can wire its
chosen storage before its initial catalog/default provider is established. Bucket/durable-origin
metadata is protected because changing it can strand historical objects or stored URLs.

## Optional transports and workers

Original AddEmail(configuration), AddMedia(configuration), AddGeo(configuration), public constructors
and strict required port validation remain available. Missing SDKs/providers still fail build-time
validation on those registrations. The new opt-in surfaces are:

- AddEmail(configuration, allowUnconfigured: true): registers the actual Resend SDK when key/sender
  are supplied, otherwise explicit typed unavailability. AddUnconfiguredEmail(configuration) chooses
  the unavailable transport deliberately. AddEmail<TProvider>(configuration, allowUnconfigured: ...)
  installs the company's IEmailProvider without supplying a vendor SDK.
- AddMedia(configuration, allowUnconfigured: true): uses a registered S3 client or explicit typed
  unavailability. AddUnconfiguredMedia(configuration) is deliberate unavailable storage.
  AddMedia<TStorage>(configuration, allowUnconfigured: ...) installs company IMediaObjectStorageProvider.
- AddGeo(configuration, allowUnconfigured: true): retains an explicitly installed provider, otherwise
  installs typed unavailability. AddGeo<TProvider>(configuration) installs the neutral geographic port.
  Active OpenStreetMap still validates identity/endpoints; an absent identity in HQ chooses unavailable
  Geo. Unconfigured Geo never reports fabricated successful empty results.
- AddNotifications(configuration, allowUnconfigured: true, useFirebase: ...): supports absent/inactive
  credentials while rejecting malformed fully configured active credentials. The original method
  retains its legacy graceful Firebase behavior. The catalog's Firebase readiness reflects actual
  credential loading, not presence of an arbitrary nonempty string.

Email/notification processors pause before claiming unready work and recheck before delivery.
A switch during a batch releases/defers unfinished work without consuming attempts, changing bodies
or declaring failure/abandonment. Email checks again after pacing and Firebase before every SDK
chunk. Media cleanup pauses without consuming cleanup attempts/quarantining objects. Sales projection
maintenance and Inbox retention stop admitting disabled work; concrete maintenance/retention calls
cannot bypass the switch. Geo detached refresh uses host shutdown plus a 60-second bound and checks
admission before fetching; the OpenStreetMap HTTP handler checks again after pacing and before the
outbound request. None of these use an unchecked detached CancellationToken.None.

Outcomes already accepted externally must still be saved. Notification delivery remains at-least-once;
recovery after a partially accepted delivery can duplicate it, as before. A toggle cannot undo a
provider request or signature already accepted. Required integration ports are never replaced with
blanket TryAdd/null fallbacks. Readiness/identity getters needed during DI construction stay readable;
actual domain/provider operations enforce admission.

## Isolated verification

Tests/ServiceCatalogTests.cs uses disposable PostgreSQL/PostGIS and Redis, loopback HTTP/MCP,
actual migrations and recording storage/email seams. It never connects a deployment or sends live
email. The focused suite covers committed selections before profile creation, protected dependencies,
limited staff navigation and current-role revocation, HTTP/MCP/direct-domain admission, paused
attempt counters and resume, provider selection/migration protections, and opt-in versus legacy
strict registration. Apps/Api/Migrations/*AddCompanyServiceCatalog adds only the independent state
table. Generated contracts/types and the minimum affected legacy checks accompany this source.
