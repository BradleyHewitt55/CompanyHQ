# UAL v1 architecture proof (#2)

This **runnable, bounded experiment** lives alongside the current C# NuGet platform. It does not replace
its API, migrations, authentication, or issue #5 Persistence boundary. Requirements/architecture are
implemented through one C# native controller and an interchangeable Python native controller, one
HTTP roof, and one independently running PostgreSQL DB service process.

## Run and verify

Requires .NET 10, Python 3.11+ and Docker. No Python packages or publishing credentials.

```sh
python Experiments/UniversalApi/verify.py
```

The runner builds Release, creates/removes its own disposable PostgreSQL container, starts **one DB
service**, runs the C# roof/service, replaces only the service implementation with Python, verifies
the same public routes and identical canonical contract/DTOs, reads data created by the other
language, exercises both native DB SDKs' CRUD/query and errors, and regenerates `contract.json`, `client.d.ts` and actual
`measurements.json`. It validates DTO fields from schema, never checks which language returned them.
Both native SDKs send calls directly to that same DB service, not through the HTTP roof.

Manual launches (use only an empty experimental DB):

```sh
# UAL_POSTGRES=Host=...;Database=ual;Username=...;Password=... in environment
# UAL_DB_PORT=47121, UAL_SERVICE_PORT=47122, ASPNETCORE_URLS=http://127.0.0.1:47123
# Build first; Python files are copied alongside the runtime.
dotnet Experiments/UniversalApi/bin/Release/net10.0/UniversalApi.dll db
dotnet Experiments/UniversalApi/bin/Release/net10.0/UniversalApi.dll api csharp
# Stop only the API process, leave DB service running, then:
dotnet Experiments/UniversalApi/bin/Release/net10.0/UniversalApi.dll api python
```

`POST /users` and `GET /users/{id}` are registered from the native controller marker and operation
markers. `GET /schema` exposes cached exported metadata. No second User HTTP API exists.

## Canonical primitive vocabulary

| UAL type | Wire form | C# native | Python native | Client |
| --- | --- | --- | --- | --- |
| string | UTF-8 JSON string | string | str | string |
| bool | JSON boolean | bool | bool | boolean |
| int32 | exact JSON integer, signed 32-bit | int | annotated int32 | number |
| int64 | exact JSON integer, signed 64-bit | long | int | bigint/string in generated wide clients |
| decimal | invariant decimal string (no float loss) | decimal | Decimal | string |
| uuid | canonical UUID string | Guid | UUID | string |
| date | YYYY-MM-DD string | DateOnly | date | string |
| datetime | RFC3339 UTC/offset string | DateTimeOffset | aware datetime | string |
| bytes | base64 string | byte[] | bytes | string |
| json | bounded JSON value | JsonNode | dict/list/scalar | unknown |
| list<T> | bounded array | List<T> | list[T] | T[] |
| optional<T> | null or value | Nullable<T>/nullable metadata | T or None | T or null |

The MVP example exports string/uuid/datetime. C# primitive mapping is implemented in the startup
adapter; Python maps the demonstrated dataclass primitives. Python also maps Annotated[int, "int32"], list[T] and optional unions recursively at startup.
Production range/precision codecs for every primitive are specified here, not claimed as fully
implemented and tested persistence SDK support. Unsupported native types fail registration instead of guessing.

## Controller, DTO and registration contract

A version-1 registration contains `service`, `prefix`, `requires`, `operations`, opt-in `dtos` and
private `models`. Each operation carries stable name, HTTP method, route template, input/output
type, errors and policy. The checked-in generated public contract is the exported DTO/route portion from **both** native
adapters, not an independently hand-authored roof-owned User schema. Private `models` are stripped
from /schema; the runner asserts that they are never client exports.

C# uses ApiService/Operation/ApiDto/DbModel attributes and startup reflection. Python uses native
controller/operation decorators and dataclass field extraction. DTOs and models are declared only
in their owning service. Models are never used as an automatic DTO export; each service explicitly
projects stored fields to its exported UserDto. TypeScript output is generated from exported `dtos`
only; the HTTP client in verify.py uses that schema independently of implementation language.

Startup:

1. Start the DB service with its pooled Npgsql data source.
2. Instantiate the selected native service, extract its controller/DTO/model metadata once.
3. Register models directly with DB (Python's SDK does this before readiness as well).
4. Register operations with the roof, cache bound C# delegates or operation names on the persistent
   Python connection, and register public HTTP routes once.
5. Serve calls with operation name + arguments, no contract/model schema on the hot path.

Conflicting model registration returns 409 and the roof refuses startup. One implementation is
selected at startup; dynamic replacement/route collisions/multi-service discovery are future work,
not per-request language selection. A deployment swaps an implementation only if its registration
is compatible; changing fields' meanings/removing fields/routes/errors requires a new contract
major version. Additive optional fields can remain in one version after consumer compatibility
checks. The runner currently demands exact equivalence for the two implementations.

## Transport decisions and protocol

C# in-process calls use cached delegates (no unnecessary process hop). Python uses a persistent
loopback TCP socket for operation RPC, **not HTTP forwarding**. Both language DB clients use a
separate persistent TCP connection to the independently hosted DB service. Npgsql pooling lives
only there. UTF-8 newline-delimited JSON is deliberately inspectable in this first slice; maximum
frame is 64 KiB, one outstanding call/connection, 10-second C# request budget. C# socket errors close
the channel so a cancelled response cannot be confused with a later call.

Service protocol: `SERVICE_REGISTER -> result(contract)` at startup, then
`CALL {operation,args} -> {result}` or `{status,error}`. The native operation is invoked directly;
there is no internal HTTP API or conventional reverse proxy. Full schema interpretation never
runs for CALL. DB protocol is separate and never routed through Central API.

This loopback demo is **trusted-process, anonymous-demo only**. Roof registration rejects another
policy rather than pretending to enforce it. Production must authenticate registration/peers,
propagate a bounded immutable caller context, enforce declared policy at the roof and enforce domain
ownership again in the native service. Do not expose DB/service sockets outside the local host.
Errors are bounded stable codes/status (400 validation, 404 missing, 409 conflict, 503 unavailable),
not exception text/SQL. No general reconnection/liveness protocol is claimed: worker death fails
calls, startup readiness is checked once and metadata remains cached.

Binary transport (e.g. MessagePack with canonical codecs), multiplexed request IDs, heartbeat/remove,
cooperative cancellation, authenticated service identity, streamed files with bounded chunks, and
backpressure are reserved future extensions. STREAM/transaction/relationship/migration negotiation
are **not** hidden in JSON or silently emulated. This MVP does not register streaming routes.

## Shared DB model and CRUD contract

`MODEL_REGISTER {model,fields}` registers an exact private model descriptor once and caches it in the
DB service. Model fields use the same primitive vocabulary; this slice uses nonnullable names/types
without index/relationship generation. A registration with a different descriptor for the same model
is rejected. There is no schema in GET/SAVE/UPDATE/DELETE/FIND/EXISTS/COUNT.

`BaseModel` includes `id`, `createdAt`, `updatedAt`. DB SAVE generates UUIDv7 if no ID was provided,
stamps creation/update times; UPDATE preserves creation and moves update time. Model data uses an
experimental generic `(model,id,created_at,updated_at,data jsonb)` table; it neither adopts nor alters
production CompanyHQ tables. A request cannot supply arbitrary SQL/table names. Models stay service
owned; both SDKs handle only type/name/serialization/protocol, not an ORM or provider engine.

| Operation | Payload | Result |
| --- | --- | --- |
| GET | model, id | model or 404 |
| SAVE | model, data | new persisted model or 409 |
| UPDATE | model, data (id required by caller) | model with timestamps or 404 |
| DELETE | model, id | boolean whether deleted |
| FIND | model, query | ordered bounded models |
| EXISTS / COUNT | model, query | boolean / count |

Private model discovery/registration is deliberately separate from DTO export. The initial
canonical query has `where`, `order`, `descending`, `limit`, `offset`. Predicate nodes are
`{op,field,value}` for eq/ne/gt/lt/contains/in, `{op:"null",field}` for null checks, or
`{op:"and"|"or",args:[...]}`. No lambdas/source code cross a socket. Queries validate fields using
cached model metadata and parameterize values; limit/offset/depth/arity are bounded. String contains
is case-insensitive literal matching, not wildcard SQL. Numeric/date ordering comparisons select
casts from metadata, never caller-provided SQL types. This is not a full query engine.

Cross-model atomic transactions would be a bounded DB `BATCH` request whose operations run in one
provider transaction; long remote session transactions and distributed transactions are rejected
as future work. Relationships/indexes/unique constraints and migrations need an explicit deployment
manifest and version negotiation before adoption; model registration must never auto-rewrite an
existing production schema. Issue #5's actual in-process provider already supports tracked multi-model
retry-safe transactions independently of this experimental transport.

## Measurements, not performance promises

`measurements.json` contains 200 sequential samples per path after 20 warmups, host/platform,
median/p95/mean milliseconds. Direct DB GET is the persistence/transport floor; public C# GET adds
HTTP roof + cached delegate + DB, and Python GET adds exactly one operation RPC hop. Cached `/schema`
is a control with no DB call, proving schema reads are a separate public operation. Measured GET series use Python's stdlib persistent HTTPConnection (not repeated TCP setup); timings
include client serialization and response decoding. PostgreSQL runs through Docker Desktop.

The observed results demonstrate the implemented short path and quantify its cost, **not production
throughput, concurrency scalability, internet latency or an unnecessary-layer-free latency theorem**.
Windows/Docker/client outliers are visible rather than discarded. Before deploying a universal
runtime, compare persistent HTTP client throughput and multiplexed channels under concurrency;
this one-call-per-channel proof intentionally does not claim that.
