# RSS

Owns safe outbound fetching, publisher metadata/discovery and RSS/Atom parsing into client-shippable
contracts. No Article/Feed domain, database, controller, URL proxy, worker, cache, ranking or refresh
policy belongs here; the caller owns validators/items and when to fetch again.

## Rules

- Every URL, DNS answer and redirect is hostile. Validate all answers at connection time and connect
  to exactly an approved address: never validate a hostname then let HTTP resolve it again.
- Refuse private, loopback, link-local, documentation, reserved and unsafe embedded IPv4 destinations.
  Check every redirect, schemes, userinfo and ports, and forbid HTTPS downgrades.
- Own the socket handler: disable automatic redirects/decompression, proxies, cookies and HTTP/3.
  Bound wire and decompressed bytes, redirects, total time, items and discovery results.
- DtdProcessing.Prohibit and a null XML resolver are mandatory. No DTD/XXE or external entity fetch.
- Changed (including empty), NotModified and Failed are distinct. Failed parsing retains caller
  validators, not the broken document's new ones. Only caller cancellation escapes as an exception.
- Result URLs are absolute HTTP(S) or null. Markup is explicitly labelled untrusted, not sanitized;
  renderers sanitize it. XML bases apply through every ancestor down to each link/media element.
- Offline fixtures replace DNS/connect, not the safety implementation. Never call a public publisher
  in tests or log response content, complete URLs, subscriber queries or validator values.

## Decisions and limits

No guessing feed paths, JSON Feed, retries, source registry or scheduler. Discovery reads the head
and never fetches the discovered feeds automatically. Public contracts have no infrastructure
references and there is no OpenAPI document because this library has no HTTP endpoint.

## Traps

A pooled socket remains pinned to its previously approved address; a new socket resolves/checks
again. RSS relative links fall back to the described site absent explicit xml:base; explicit bases
resolve from the document. Unknown dates are null. Named XML entities without a declaration are
malformed; even harmless historical DOCTYPE feeds are refused. The offline fixture is plain HTTP,
so HTTPS certificate rejection/downgrade enforcement is reviewed code, not certificate-fixture
coverage. Byte/entry limits may intentionally truncate results.
