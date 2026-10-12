# Moderation service

`Services/Moderation` owns reports, source-owned hidden content, user blocks, account flags, and the
read-only global restriction union. It references only foundation (Persistence, Postgres.Contracts, Cache, User, Auth
contracts) and never a service that owns reportable content. A source is plugged in as an
`IReportableContentSource` bridged in `Apps/Api/Integrations` (Message's is there); Moderation
discovers every registered source through its scoped registry and owns only the `user` source itself.

## Rules

- The persisted entities are `UserBlock`, `ContentReport`, `ReportedContentSnapshot`,
  `HiddenContent`, `AccountFlag`, and append-only `ContentReportDecision`.
- `ReportTargetRegistry` is the only report target allowlist. It normalizes kinds, rejects duplicate
  registrations, validates each source's immutable reason catalog, and drives both options discovery
  and create/admin filtering. An adapter result explicitly distinguishes a missing target from a
  target that has no snapshots. Adapter failures other than cancellation are logged and still allow
  a pending report with no evidence.
- A submitted reason is an allegation. Creation makes a pending queue item and a `Source=Report`
  hide for that reporter; it never globally suppresses, deletes, classifies, or automatically blocks
  the target. `Other` requires nonblank details. There is no AI/provider integration in this lane.
- `ContentReportDecision` is append-only. Allowed transitions are exactly:
  `Pending -> Reviewed | ActionTaken | Dismissed`, `Reviewed -> ActionTaken | Dismissed`,
  `ActionTaken -> Dismissed`, and `Dismissed -> ActionTaken` with a nonblank reversal note.
  `Reviewed` changes the selected allegation. `ActionTaken` and `Dismissed` apply to every report
  currently linked by `(TargetKind, TargetId)` in one serializable execution-strategy transaction,
  with one history row per changed report. Statuses, histories, and report-owned hide rows commit
  atomically. The admin UI requires explicit reversal confirmation; post-commit hidden and
  restriction-cache invalidation is not skipped by request cancellation.
- A reporter may withdraw only their own pending post or article report. Withdrawal marks the report
  `Withdrawn`, preserves its snapshots and existing audit history, removes only the matching
  `Source=Report` hide, and leaves administrator decisions authoritative. The reporter/target
  uniqueness index excludes withdrawn rows so a later report can be filed.
- `ActionTaken` is reversible suppression, not deletion. `IContentRestrictionService` reads and
  caches the union of target ids whose reports remain `ActionTaken`; content services apply that
  union only to non-admin client reads. Admin reads and frozen snapshots remain available.
  Target-wide dismissal removes all linked report-owned hides and restores the existing content;
  reports and histories remain archived audit evidence instead of being physically deleted.
- `HiddenContent` carries `Source` and `SourceId`. Report, block, manual, and conservative legacy
  causes can coexist for one caller/target. Public unhide removes only manual/legacy causes and
  never removes an active block or report cause. Unblocking removes the exact persisted block-owned
  row; no timestamp inference is allowed. Legacy migration rows retain an independent legacy
  ownership marker so deployment cannot reveal existing hidden targets.
- `NotInterested` is Post/Feed recommendation state, not Moderation. Moderation never writes a
  hidden row for it.
- `AccountFlag (UserId, Kind)` is unique only while `Status = 'Active'`. The partial index is
  intentional: a cleared or upheld flag must not prevent a later active flag.
- `BlockedForAsync` returns the union of accounts blocked in either direction. The public blocks
  list is deliberately outbound-only; an inbound block is never disclosed to the blocked account.
- Reports snapshot before the source is allowed to disappear. Every source supplies only safe
  operator evidence and a nullable owner id where ownership is absent.
- Hidden, block, flag, and restriction reads use a five-minute cache and invalidate the exact affected
  keys after committed writes. Postgres reports remain authoritative.
- DTOs are audience-specific. Read methods take an explicit `ResponseAudience`, default to the
  client shape, and never use audience as a substitute for authorization: the flag queue, the report
  queue and every decision need `moderation.manage`, rechecked in the service. Admin details compute the
  target report total and return every linked report with its `ReporterUserId`, reason, details,
  status, and creation time. Client DTOs do not expose report details, reporters, evidence,
  snapshots, decision history, or inbound block relationships.
- Report creation is per-account rate limited to 20 requests per hour. Duplicate reports are
  idempotently rejected and self-targets are rejected.

## Deliberately not built

- No automatic moderation action, classifier, mute, appeal workflow, role change, account deletion,
  operator-raised flag endpoint, or user account sanction from an `ActionTaken` user report.
- No discovery endpoint for inbound blocks.
- No moderation dependency on any feature service. The host bridges each one in.
