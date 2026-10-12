# Redis

Owns the Redis SDK, options, shared multiplexer and transport behind Cache.Contracts' provider port.
It owns no namespace, serialization, expiry policy, feature rule or fail-silently decision. No
reference to Cache implementation and no SDK/connection in Cache or its feature consumers.

## Rules

- Bind existing Cache configuration, carry qualified keys/payloads/TTLs unchanged, and keep Web's
  independent refresh-token keys readable without migration.
- Preserve provider exception identity. Classify transport failures only; command/validation errors
  must not be silently swallowed by Cache.
- Exercise the adapter with disposable Redis, not a simulated provider. Scripted providers test only
  Cache policy.
- Keep one lazy singleton multiplexer; retry/reconnection belongs to the SDK, not a fallback cache.

## Decisions and limits

No pub/sub, lock service, streams or alternate cache. Host registration is explicit in addition to
AddCache. Connection syntax belongs here; Cache retains its opaque ConnectionString property but
cannot retain the old SDK-returning BuildConfiguration method without violating neutrality.

## Traps

Prefix deletion is a glob, skips replicas/disconnected servers and reports already-deleted counts
on partial failure. Preserve scan/delete batching. Redis commands cannot abort an in-flight round
trip with a token; prefix scanning observes cancellation between pages. Counters increment and set
the first expiry atomically and round their window to whole seconds. Keep AllowAdmin and
AbortOnConnectFail policies pinned as before.
