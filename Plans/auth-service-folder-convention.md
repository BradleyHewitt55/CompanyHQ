# Auth service-folder convention

## Goal

Move Auth injectable classes into `Services/` using the established service naming and namespace convention.

## Scope

- Move and rename PasswordGenerator, UserDirectory, AdminUserSeeder, and LoginRateGate to `Services/*Service.cs`.
- Move CommonPasswordValidator to `Services/` without changing its framework-conventional type name.
- Move AuthRoles to `Models/`.
- Update DI, endpoint signatures, pages, imports, and tests.

## Task-specific tests

- Build Auth and run its tests after the renames.
- Grep for obsolete Auth type names outside their replacement files.
- Exercise owner bootstrap and admin user endpoints to verify their updated dependencies resolve.
- Assert AuthRoles resolves from `Services.Auth.Models`.
