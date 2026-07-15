# Shared Postgres company isolation

## Goal

Use one server-level Postgres container with a separate database and least-privilege role for every company.

## Scope

- Provision and remove one database and role per company.
- Store only the owning company connection string in its protected deployment configuration.
- Ensure company roles cannot connect to or query another company database.

## Task-specific tests

- Provision two companies and assert one Postgres service, two database names, and two role names.
- Connect using each company role and verify access to its own database succeeds.
- Verify each role cannot connect to or query the other company database.
- Assert generated application configuration contains only its own connection string and no shared Postgres superuser credential.
