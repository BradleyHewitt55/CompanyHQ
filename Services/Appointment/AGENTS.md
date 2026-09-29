# Appointment

A member's own appointment diary. Each row belongs to one account and only that account can see it.
Appointment depends on nothing but Postgres, User (the account foreign key) and Auth's claim helpers.
It knows no other feature service.

## Two ways in

`IAppointmentService` is the member's CRUD, reached over HTTP. `IAppointmentScheduler` is the port
another service calls to keep a row in step with something it owns. They are separate on purpose: a
calling service can create, move, and cancel the rows it owns, and can do nothing else to a diary.

The scheduler port lives in `Contracts/Ports/`, the dependency-free assembly. A future caller does not
reference Appointment: the host bridges the caller's own port to `IAppointmentScheduler` in
`Apps/Api/Integrations`, as it does for every pair of feature services. Nothing in Appointment
changes to admit a new caller.

`SourceKind` is free text rather than an enum for the same reason. An enum would mean editing this
service every time another one wants to schedule. Use the calling entity's name.

## Rules a change must not break

- **The `UserId` column is the only authorization.** Every read and write filters on the acting user.
  A method that reaches the table without that filter is a hole.
- **Someone else's id is a 404, not a 403.** A 403 confirms the id exists. The controller maps the
  service's `null` to `NotFound` for exactly this reason.
- **A source-linked row is read-only to the member.** Update and delete filter on `SourceKind == null`,
  and `CanManage` on the DTO tells the client why a row has no edit button.
- **`(SourceKind, SourceId)` is unique, and that constraint *is* the idempotency.** `ScheduleAsync`
  upserts on it, so a caller may run it on every save. Do not replace the unique index with a
  check-then-insert: two concurrent saves of the same source row would both create an appointment.
- **`ScheduleAppointmentRequest` is never bound from a request body.** `SourceKind` and `SourceId`
  are the calling service's identity. A member who could set them could overwrite another service's
  rows.
- **The scheduler goes through the repository and opens no transaction.** It commits on its own
  save, not the caller's; a caller that needs the two to land together must call it last.
- **Deleting an account cascades its diary away.** The `UserId` foreign key uses
  `DeleteBehavior.Cascade`; do not work around it with a soft delete.
- **`AppointmentProjections` is the only place the entity becomes a DTO.**

## Decisions

**Private by default, no sharing.** A diary row has one reader. Sharing an appointment with other
accounts is a join table and a second authorization rule; build it when a real caller needs it.

**No status field.** "Done" is derivable from `ScheduledAt` against the clock. Add a status only when
something other than the calendar needs to distinguish *missed* from *past*.

**No admin DTO.** The shape describes the caller's own diary to them. Per `Services/AGENTS.md`, an
admin variant that adds nothing implies a boundary that is not there.

**Past dates are allowed.** Members log things after the fact. The only rejected `ScheduledAt` is one
that is not a real instant, enforced in `AppointmentValidation` and as a check constraint.

## Known limits

- The list is paged and split by `upcoming` (default true). There is no date-range filter; add
  `from`/`to` when a caller needs it.
- There is no all-day flag, so a caller scheduling from a date anchors it to midnight UTC.
- Nothing notifies. There are no reminders and no calendar sync.
