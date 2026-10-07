# Service regression tests

- Use disposable PostgreSQL/PostGIS and Redis containers; never connect tests to a deployment.
- File-backed object storage exercises media bytes without external vendor credentials.
- Keep tests focused on authorization, persisted state transitions, and boundary behavior.
