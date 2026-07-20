# User Model

## Decision

One implemented, sealed [`User`][1] model provides platform identity and role-based
access; plugins extend users by composition, never inheritance.

- **Fields:** `Id`, `Name`, `Surname`, `Email`, `ContactNumber`, `Role`, and
  `PasswordHash`. `Email` has a unique index.
- **Roles stay strings.** [`Roles.Admin`][2] identifies company administrators and `Roles.Client` identifies users registered by a company site; there is no role table.
- **Passwords stay private.** [`UserService`][3] hashes seeded users, [`AuthService`][4]
  hashes registrations and verifies login credentials. Auth responses use [`AdminUserDto`][5] or
  [`ClientUserDto`][6]; both include every current user field except `PasswordHash`.
- **One password rule.** [`PasswordPolicy`][7] (8-128 characters, length-only) lives in
  Abstractions so the web form, service layer, and CLI prompt enforce the same rule.
- **CRUD stays provider-neutral.** [`IUserService`][8] exposes list, lookup, query,
  upsert, and delete operations. A null `UserSave.Id` creates a user; an existing id
  updates one, and an omitted update password preserves its hash.
- **Duplicate writes have one contract.** [`PostgressRepositories`][9] translates
  PostgreSQL unique violations into [`UniqueConstraintException`][10], so services and
  controllers never depend on Npgsql and concurrent duplicate inserts return `409`.
- **Extension is composition-only.** A plugin stores its own model with a
  `Guid UserId`; the base user remains sealed and minimal.

[1]: ../../Projects/Abstractions/Users/User.cs
[2]: ../../Projects/Abstractions/Users/Roles.cs
[3]: ../../Projects/Services/Auth/Services/UserService.cs
[4]: ../../Projects/Services/Auth/Services/AuthService.cs
[5]: ../../Projects/Services/Auth/Dtos/AdminUserDto.cs
[6]: ../../Projects/Services/Auth/Dtos/ClientUserDto.cs
[7]: ../../Projects/Abstractions/Users/PasswordPolicy.cs
[8]: ../../Projects/Abstractions/Users/IUserService.cs
[9]: ../../Projects/Services/Postgress/Services/PostgressRepositories.cs
[10]: ../../Projects/Abstractions/Data/UniqueConstraintException.cs
