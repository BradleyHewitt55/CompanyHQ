# Company developer workspace isolation

## Goal

Ensure separate company workspaces are independently editable without central-application access or cross-company file/data access.

## Scope

- Support separate developer workspaces rooted at each company directory.
- Keep company services, pages, files, and database credentials isolated.
- Do not require developers to query a central CompanyHQ application to add or modify a company service.

## Task-specific tests

- Open separate Acme and Beta developer workspaces.
- Add or modify a service in each workspace and assert only the owning company reflects the change.
- Assert neither workspace can read the other company's files or deployment configuration.
- Assert neither developer workflow needs a central CompanyHQ application query.
