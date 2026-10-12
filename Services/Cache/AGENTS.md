# Cache

Owns the domain-facing cache abstraction, qualified namespaces, JSON payloads, TTLs, null caching,
cache-aside and failure policy. The host composes the Cache-owned provider port with Redis; Cache
owns no SDK, connection, transport exception type, entity, endpoint or invalidation schedule.

## Rules

- No Cache -> Redis dependency or provider SDK. Feature callers depend only on Cache; Redis depends
  only on Cache.Contracts. Required adapters fail host validation when absent, with no memory fallback.
- Preserve existing configuration/keys/payloads without a flush. The adapter gets already qualified
  keys, serialized payloads and exact TTLs and must never add its own namespace/policy.
- All entries expire. Cache DTOs, never entities, tokens, credentials or raw precise device locations.
  Include audience/language/page variants in keys when the result varies; Cache cannot infer them.
- FailSilently swallows only JSON/timeouts/provider-classified transport failures, never usage or
  validation bugs. Counters report Unavailable on failure so security callers can fail closed.
- Health is Degraded rather than Unhealthy on outage. Geo cache misses can amplify upstream pacing;
  cache unavailability is not permission to bypass a provider rate limit.
- Test policy with scripted ports, but exercise Redis transport against a disposable real container.

## Decisions and limits

No lock, pub/sub, memory fallback, tag engine or implicit feature invalidation. GetOrSet caches null
negatives and takes no stampede lock: factories must be safe to run twice. Host registration adds
Cache and Redis separately. The old SDK-returning CacheOptions.BuildConfiguration cannot live here;
its deliberate major-version migration is described in Docs/provider-upgrade.md.

## Traps

Pass bare keys; double-qualification silently orphans entries. Prefix deletion is glob matching,
not a path segment: post:1 matches post:10, and metacharacters are not escaped. Cancellation may only
be observed at entry/scan pages, not during SDK commands. Preserve partial deletion counts. Web's
independent Node refresh coordination shares companyhq:web:refresh:* on the same Redis instance;
feature invalidation must never sweep web: keys. Serializer changes do not evict old payloads:
version incompatible shapes deliberately and let existing entries expire.
