# Universal API experiment

This bounded C#/Python vertical slice belongs to issue #2, not the production NuGet platform or
issue #5's in-process Persistence refactor. It must never be enabled in company hosts implicitly.

## Rules

- Native controllers/models/DTOs and their validation stay with each service. The roof sees cached
  registration metadata and invokes an operation, never an internal HTTP URL.
- Only explicitly exported DTOs become client schema. Model registration goes directly to the DB
  service and is never resent on CRUD/CALL. Both languages use the same live DB service/process.
- Use loopback-only bounded persistent RPC for this experiment. It is unauthenticated development
  plumbing, not a deployable network trust boundary. Refuse unsupported auth policy registrations.
- Verification owns only freshly named disposable containers and temporary processes; never point
  the test at CompanyHQ or a consumer's database. The experimental generic record table is separate.

## Decisions and limits

JSON/TCP is the measurable v1 proof, not a finalized binary production wire protocol. One outstanding
call per connection intentionally bounds the slice; no proxy, schema service, discovery per request,
service mesh or distributed transaction. Run verify.py to regenerate contract/client declarations and
measurements together. Record observations honestly, including client/host overhead and outliers;
no latency targets or production scalability claims from a loopback sample.

## Traps

A cancelled RPC closes its connection to prevent response misassociation. Reflection/decorators run
at startup; no per-call schema extraction. HTTP client measurements include client connection costs
and PostgreSQL I/O, not just routing. Model BaseModel timestamps/ids are assigned by the DB service.
The experimental anonymous-demo policy must not become a default for production service registration.
