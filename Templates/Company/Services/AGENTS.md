# Company services

- Company-specific domain services and contracts stay here; reusable generic services remain in HQ.
- Register company services in the company API host and preserve them during generic dependency updates.
- Use generic services through their CompanyHQ packages; never copy their source into this company.
- Keep client contracts free of database and provider implementations; publish HTTP contracts from the company host.
- Every service has a generated `Contracts/OpenApi/{name}.json`, refreshed by every dev or production start. Never hand-edit it or drop it from the build.
- Enforce ownership in business logic as well as authenticating HTTP callers.
- Verify behavior in the `Tests` xUnit project against disposable backing services; do not add test runners to Scripts.
