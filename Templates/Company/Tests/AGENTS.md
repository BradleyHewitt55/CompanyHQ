# Tests

- One xUnit project, flat in this folder, the same flow CompanyHQ uses. Run with `dotnet test Tests/Company.Tests.csproj`; Docker must be running.
- Tests boot the whole company API in memory through `ApiFactory` against disposable Postgres and Redis containers. Never point a test at the development stack or a deployment.
- Test company services only. Generic service behavior is tested in CompanyHQ before a package is published.
- New tests join this project; do not add another runner, project, or script.
