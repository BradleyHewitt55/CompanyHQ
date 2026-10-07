# Cache service

The only place CompanyHQ talks to Redis. Others inject `ICacheService` — never a connection string,
never `StackExchange.Redis`, never `IMemoryCache` as a substitute. It owns the connection, key
namespace, payload format, and expiry policy.

It deliberately owns **nothing else**: no entity, no table, no endpoint, no `.http`, no DTO, no
invalidation logic. Deciding *when* `post:{id}` is stale is the owning service's business. Its only HTTP
surface is the `cache` entry in `GET /health`.

## Rules

- Cache is a shared foundation dependency: feature services may reference it for `ICacheService`.

- No `IConnectionMultiplexer` or `StackExchange.Redis` outside this project.
- No cache entry without a TTL. There is no never-expires overload, on purpose.
- **No entity in the cache.** DTOs only — Redis is the wire in all but name.
- **The audience must be in every key.** Cache has no DTO split of its own and cannot tell an admin
  payload from a client one, so a key missing `ResponseAudience` will serve admin JSON to a client
  and nothing will detect it. Same for anything else that varies a response: language, page.
- Never cache a location, password hash, refresh token, or JWT. Redis has no row-level access
  control and its dump is unencrypted.
- Cache is never the source of truth. Survives-a-restart means Postgres.
- A cache failure must never become a 5xx while `FailSilently` is on.
- No mock data — exercise against the real `redis:7.4-alpine` container.

Key shape: `{entity}:{id}:{variant}`. Group by the thing that gets invalidated so one
`RemoveByPrefixAsync` clears everything derived from it. Pass **bare** keys; `Cache:KeyPrefix` is
applied for you, and qualifying it yourself double-prefixes.

## DTOs: none, and it should stay that way

It owns no persisted entity, so there is no row to have a narrow and a privileged view of — the
`{Role}{Class}Dto` split has nothing to apply to. `CacheOptions` is an options class, never
serialised to a caller. `ICacheService` is generic over `T` precisely so the *calling* service's
DTOs pass through untouched.

## Traps

- **`GetOrSetAsync` caches negatives.** A factory returning `null` is recorded as a hit and served
  for the full TTL. If a wrong `null` is expensive, do not cache it long.
- **`GetOrSetAsync` takes no lock.** Concurrent callers all run the factory; it must be safe to run
  twice and cheap enough to survive a small stampede.
- **The cancellation token is checked on entry, then dropped.** It is never passed to
  StackExchange.Redis, so a cancelled request cannot abort an in-flight round trip. Only
  `RemoveByPrefixAsync` honours it, between scan pages.
- **The prefix is a glob prefix, not a path segment.** `post:1` matches `post:10`. End prefixes at a
  `:`. Metacharacters are not escaped — keep `*`, `?`, `[` out of keys.
- **`SCAN` needs `AllowAdmin = true`**, which `CacheOptions.BuildConfiguration` sets for that reason
  alone. A Redis that forbids admin commands breaks `RemoveByPrefixAsync` and nothing else.
- **Only `RedisException`, `JsonException`, `TimeoutException` are swallowed.** A null key or bad `T`
  throws regardless of `FailSilently` — caller bug, not an outage.
- **Changing serializer options invalidates nothing.** Old-shape entries live out their TTL. Bump
  `Cache:KeyPrefix` at the same time or accept silent misses.

## Configuration

Section `Cache`; from the environment, `Cache__ConnectionString` and so on. Bad config throws inside
`AddCache`, not on first read.

| Key | Default | Notes |
| --- | --- | --- |
| `ConnectionString` | `localhost:6379` | The StackExchange.Redis configuration string, and the **only** place the endpoint, `password=`, `defaultDatabase=`, `ssl=`, and timeouts are set. There are deliberately no separate host/port/password keys to disagree with it. |
| `KeyPrefix` | `companyhq:` | Also bounds what `RemoveByPrefixAsync` can scan. |
| `DefaultTtlSeconds` | `300` | Must be > 0. |
| `ScanPageSize` | `250` | Keys per `SCAN` and the delete batch size. Must be > 0. |
| `FailSilently` | `true` | Redis errors become misses. |

`AbortOnConnectFail = false` and `AllowAdmin = true` are pinned in code on top of the parsed string,
so no connection string can switch them off.

The compose Redis requires `REDIS_PASSWORD`; `docker-compose.yml` composes it into the API's
`Cache__ConnectionString` and the Web app's `WEB_REDIS_URL`. Web shares the instance for its
refresh-token coordination and writes `companyhq:web:refresh:*`, which lands inside this service's
prefix — so a bare key here must never start with `web:`.

## Failure behaviour

`FailSilently` is on by default: Redis errors, timeouts, and undeserialisable payloads log at
warning and report a miss. The health check reports **Degraded**, not Unhealthy — a cacheless host
still serves, and failing readiness on a Redis blip is a self-inflicted outage. The multiplexer sets
`AbortOnConnectFail` off so a cold Redis does not block startup.

That is not free: a miss falls through to the factory, and a rate-limited factory (`Services/Geo`
against Nominatim, one request per second) turns a Redis outage into a rate-limit problem. Turn
`FailSilently` off only in a test asserting on Redis behaviour.

## Decisions

| Decision | Why |
| --- | --- |
| Redis, not `IMemoryCache` | Two hosts must not disagree about a value, and a deploy must not cold-start every entry. |
| JSON payloads | Readable in `redis-cli` mid-debug. Binary saves bytes we are not short of. |
| Singleton service and multiplexer | The multiplexer is thread-safe and multiplexes by design. |
| Mandatory TTL | An entry with no expiry outlives the bug that created it. |
| No project references | Cache sits at the bottom of the graph so anything may depend on it, including services that must not see Postgres. Auth, Geo, Message, and Moderation consume it. |

Deliberately **not** built:

- **No distributed lock / stampede protection.** A held lock outliving its owner is a worse failure
  mode than running a factory twice. Revisit when a factory is genuinely expensive.
- **No tag or dependency-graph invalidation.** Key prefixes are the tagging scheme.
- **No `IDistributedCache`.** Nothing asks for it, and its `byte[]` surface would flatten the typed
  one for no caller.
- **No per-entity cache-aside helpers.** Invalidation belongs to the service owning the entity;
  "invalidate everything about a post" here would put that service's rules in the cache project.
