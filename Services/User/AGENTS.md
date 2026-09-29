# User service

`Services/User` owns account records, saved access roles, the seeded administrator, profile DTOs,
and account deletion. Auth owns tokens and sign-in; MCP owns connection credentials.

## Rules

- Every person has an `AccessRoleId`. `AccessRole` stores one name and an explicit permission array.
  The seeder creates `Client` and `Admin` rows and backfills legacy accounts. New roles start
  with the Client permissions. A legacy null assignment uses the old enum until backfilled.
- An authorized operator can type a role name while creating or editing a user. An existing name
  assigns the same row; a new name creates a Client-permission row. `api/access-roles` edits the
  shared permissions. The next JWT or agent-key request reloads that row from Postgres.
- `users.manage` controls named account writes. `users.read_private` separately controls private
  profile data and is also required for writes that return `AdminUserDto`. `roles.manage` controls
  role list and edits. A caller cannot gain a private DTO by only having a write permission.
- A user manager may change or delete only an account whose current permissions fit within their
  own, and may assign only a role within that same ceiling. Role editors may grant only permissions
  they currently hold; the built-in Client and Admin rows cannot be edited.
- `UserProjections` keep password hashes out of SQL read projections and all DTOs. Public and owner
  responses must never add an email address or audit stamp by accident.
- The self-service profile route takes its identity from the token and never accepts a role, email,
  password, or arbitrary user id. Account deletion always goes through
  `AccountDeletionCoordinator` and checks every guard before releasing any rows.

## Decisions

- The old `UserRole` text column remains for compatibility with callers and legacy code. Custom
  named roles use `Client` there; permissions on the linked role row are authoritative for delegated
  functions. Some operations still require the built-in Admin role because no delegation was intended.
- Account and connection disabled-tool arrays are narrowing controls. Role permissions are grants;
  removing a disabled-tool name never grants a permission missing from the role.
- `AdminSeeder` never reapplies the configured password. It repairs verification and role assignments
  and fails if the configured admin email belongs to a non-admin account.

## Traps and wiring

- `AddUsers` follows `AddPostgres` and precedes `AddAuth`. Auth cannot be referenced from User without
  creating a project cycle, so authorization claims use the shared `permission` claim name.
- The `AccessRole` entity and `User` entity are discovered because the API references this project. The role migration must preserve tables for services no longer present in the host.
- Named account writes resolve or create the role inside the same serializable transaction that
  rechecks the operator's current `users.manage` permission.
