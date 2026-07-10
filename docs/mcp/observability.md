# MCP observability

> **Document status:** Target V1 operational contract.

Trace session and call spans across transport, auth, policy, handler, application command/query, outbox, and downstream work. Metrics cover calls, latency, denials, schema failures, cancellations, timeouts, retries, rate limits, sessions, bytes, and tool outcomes by low-cardinality tool/risk class.

Logs are structured and redacted. Agent/user/task IDs belong on traces/logs or controlled exemplars, not unbounded metric labels. Alerts cover authorization anomalies, audit sink failure, cross-installation token attempts, retry storms, and mutation-tool error rates.
