# User service

`Services/User` owns account records, saved access roles, the seeded administrator, profile DTOs,
and account deletion. Auth owns tokens and sign-in; MCP owns connection credentials.

## Rules

- User is a shared foundation dependency for account lookup, role authorization, and response
  audience resolution. Feature services may reference it without coupling to another feature.
- Restricted service operations recheck
  `IUserAuthorizationService.HasPermissionAsync(actingUserId, permission)` against the current role
  row. Controller authorization alone cannot protect in-process callers.
- Controllers resolve wider responses with `User.Audience(AccessPermissions.X)`: the matching
  permission yields Admin, another signed-in caller gets Owner, and an anonymous caller gets Client.
  Keep this helper in User's `Extensions/`; do not hard-code Admin for a restricted route.

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
  they currently hold; the built-in Client and Admin rows cannot be edited or deleted. A role is
  deleted only when no account and no agent connection still points at it (409 otherwise): accounts
  are never moved to another role as a side effect.
- **Client and Admin are the only built-in roles.** Everything else, including a role meant for an
  agent connection, is an ordinary row an operator creates and edits. Never add a third role name to
  the code.
- `GET /api/users?field=&keyword=` needs `users.read_private`: only an operator may search the
  directory by text. With a field it is the shared search over `AdminUserDto`; with none it keeps
  the directory's own match, the exact address or any part of the name.
- `UserProjections` keep password hashes out of SQL read projections and all DTOs. Public and owner
  responses must never add an email address or audit stamp by accident.
- The self-service profile route takes its identity from the token and never accepts a role, email,
  password, or arbitrary user id. Account deletion always goes through
  `AccountDeletionCoordinator` and checks every guard before releasing any rows.

## Decisions

- The old `UserRole` text column remains for compatibility with callers and legacy code. Custom
  named roles use `Client` there; permissions on the linked role row are authoritative for delegated
  functions. No operation checks that column or the role name: `IUserAuthorizationService` answers
  `HasPermissionAsync` for one account and `ListUserIdsAsync` for everyone holding a permission, and
  both read the role row.
- Account and connection disabled-tool arrays are narrowing controls. Role permissions are grants;
  removing a disabled-tool name never grants a permission missing from the role.
- `AdminSeeder` never reapplies the configured password. It repairs verification and role assignments
  and fails if the configured admin email belongs to a non-admin account. On every boot it resets the
  built-in `Admin` and `Client` rows to the permission lists in code and puts the seeded administrator
  back on the `Admin` row, so a permission added to `AccessPermissions.All` reaches that account
  without a migration. Custom roles are never touched.

## Traps and wiring

- `AddUsers` follows `AddPostgres` and precedes `AddAuth`. Auth cannot be referenced from User without
  creating a project cycle, so authorization claims use the shared `permission` claim name.
- The `AccessRole` entity and `User` entity are discovered because the API references this project. The role migration must preserve tables for services no longer present in the host.
- Named account writes resolve or create the role inside the same serializable transaction that
  rechecks the operator's current `users.manage` permission.
