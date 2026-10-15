# Appointment

Appointment diaries. Each row belongs to one account; that account reads and edits it, and so does
any caller whose role grants `appointments.manage`. Appointment depends on neutral Persistence and shared wire contracts,
User (the account foreign key and the permission check) and Auth's claim helpers. It knows no other
feature service.

## Three ways in

- **A member**, over HTTP: `IAppointmentService` scoped to their own rows. This is what a
  client-facing app calls.
- **Staff**, over the same routes: a role with `appointments.manage` lists every diary (or one, with
  `?userId=`), books in another account's diary by naming `UserId`, and gets `AdminAppointmentDto`,
  which adds whose diary the row is in. A deployment that only wants appointments in the admin
  console uses the service this way and ships no member UI.
- **Another service**, in process: `IAppointmentScheduler`, the port a calling service uses to keep a
  row in step with something it owns. It can create, move, and cancel the rows it owns, and can do
  nothing else to a diary.

The scheduler port lives in `Contracts/Ports/`, the dependency-free assembly. A caller does not
reference Appointment: the host bridges the caller's own port to `IAppointmentScheduler` in
`Apps/Api/Integrations`, as it does for every pair of feature services. Nothing in Appointment
changes to admit a new caller. No service in this repository calls it yet; it is the extension point
a company's own service plugs into.

`SourceKind` is free text rather than an enum for the same reason. An enum would mean editing this
service every time another one wants to schedule. Use the calling entity's name.

## Rules a change must not break

- **The owner filter is the authorization.** Every read and write goes through `Reachable`, which
  filters on the acting user unless the service itself has confirmed `appointments.manage` from the
  role row. A method that reaches the table any other way is a hole.
- **An id out of reach is a 404, not a 403.** A 403 confirms the id exists. The one 403 is a member
  naming another account's `UserId` on create.
- **A source-linked row is read-only to members and staff alike.** Update and delete filter on
  `SourceKind == null`, and `CanManage` on the DTO tells the client why a row has no edit button.
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
- **`AppointmentProjections` is the only place the entity becomes a DTO**, and `Shape(audience)` is
  what a `?field=&keyword=` search may name. The staff shape's owner fields are aliased to the
  account row there.

## Decisions

**Repeat creation.** Daily, weekly, and monthly booking creates 1 to 100 independent rows in one
save. Monthly dates stay anchored to the original day (January 31, February 28, March 31).
The create response is the first row; list the diary to see all occurrences. Updates and deletion
affect only one occurrence. This is finite booking, with no recurring-series scheduler.

**No sharing.** A diary row has one owner. Sharing an appointment with other accounts is a join
table and a second authorization rule; build it when a real caller needs it.

**No status field.** "Done" is derivable from `ScheduledAt` against the clock. Add a status only when
something other than the calendar needs to distinguish *missed* from *past*.

**Past dates are allowed.** Members log things after the fact. The only rejected `ScheduledAt` is one
that is not a real instant, enforced in `AppointmentValidation` and as a check constraint.

## Known limits

- The list is paged and split by `upcoming` (default true). There is no date-range filter; add
  `from`/`to` when a caller needs it.
- There is no all-day flag, so a caller scheduling from a date anchors it to midnight UTC.
- Nothing notifies. There are no reminders and no calendar sync.
- An appointment cannot be moved to another account's diary; delete it and book it again.

## Runtime catalog constraints

Catalog admission applies to diary and scheduler calls as well as HTTP. Disabling keeps bookings and source links intact; enabling does not replay or duplicate them.
