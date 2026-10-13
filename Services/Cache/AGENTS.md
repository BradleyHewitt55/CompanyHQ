# Cache

Owns the domain-facing cache abstraction, qualified namespaces, JSON payloads, TTLs, null caching,
cache-aside, failure policy and the Redis commands that carry them, through the official
`StackExchange.Redis` package. It does not own the connection: the host builds and registers the
`IConnectionMultiplexer`. It owns no entity, endpoint or invalidation schedule.

## Rules

- Feature callers depend only on `ICacheService`, never on the multiplexer. Cache.Contracts stays
  free of the Redis package. A host without a registered `IConnectionMultiplexer` fails validation
  at startup; there is no memory fallback.
- Preserve existing configuration/keys/payloads without a flush. The provider gets already qualified
  keys, serialized payloads and exact TTLs and must never add its own namespace/policy.
- All entries expire. Cache DTOs, never entities, tokens, credentials or raw precise device locations.
  Include audience/language/page variants in keys when the result varies; Cache cannot infer them.
- FailSilently swallows only JSON/timeouts/`RedisException`, never usage or validation bugs. Counters
  report Unavailable on failure so security callers can fail closed.
- Health is Degraded rather than Unhealthy on outage. Geo cache misses can amplify upstream pacing;
  cache unavailability is not permission to bypass a provider rate limit.
- Test policy with a scripted `ICacheProvider`, but exercise the Redis commands against a disposable
  real container, not a simulated one.

## Decisions and limits

No lock, pub/sub, streams, memory fallback, tag engine or implicit feature invalidation. GetOrSet
caches null negatives and takes no stampede lock: factories must be safe to run twice. There is no
CompanyHQ Redis package: it only wrapped a client StackExchange already publishes. `ICacheProvider`
remains as the seam between the cache policy and Redis so the policy can be tested without it.
`Cache:ConnectionString` stays the connection key and Cache still refuses to register without it,
but the host parses it; retry and reconnection belong to the SDK.

## Traps

The host must build the multiplexer with `AbortOnConnectFail = false` and register it as a lazy
singleton before `AddCache`. With the SDK default, a Redis that is down when the first request
arrives makes the connection throw for every caller instead of degrading to misses.

Pass bare keys; double-qualification silently orphans entries. Prefix deletion is glob matching,
not a path segment: post:1 matches post:10, and metacharacters are not escaped. It skips replicas and
disconnected servers and keeps its scan/delete batching. Cancellation may only be observed at
entry/scan pages, not during SDK commands. Preserve partial deletion counts. Counters increment and
set the first expiry atomically and round their window up to whole seconds. Web's
independent Node refresh coordination shares companyhq:web:refresh:* on the same Redis instance;
feature invalidation must never sweep web: keys. Serializer changes do not evict old payloads:
version incompatible shapes deliberately and let existing entries expire.
