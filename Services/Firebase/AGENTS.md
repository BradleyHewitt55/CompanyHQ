# Firebase service

`Services/Firebase` is CompanyHQ's Firebase Cloud Messaging transport. It owns the optional
service-account credential, the named `FirebaseApp`, and per-token FCM delivery outcomes. It does
not know users, devices, notification rows, or Postgres.

## Rules

- Keep `CompanyHQ.Services.Firebase` at the bottom of the dependency graph. It references no other
  CompanyHQ service.
- The host calls `AddFirebase()` once. Firebase's named app creation throws when a second
  registration uses the same app name, so never call it from another service's registration.
- Missing credentials log one warning and disable sending; they never prevent the host from
  starting. The service reports `IsConfigured == false` in that state. An unconfigured send returns `PermanentFailure` for every token.
  Retrying an unconfigured provider is a retry storm, not resilience.
- Payloads are data-only. Title and body are fields in the data map, never an FCM notification
  payload, so Android foreground and background delivery use the same deep-link path.
- Requests are chunked at the 500-token multicast limit. Return one outcome per token.
  `UNREGISTERED` or `NotFound` becomes `TokenDead`, network or timeout or provider 5xx becomes
  `TransientFailure`, other provider failures become `PermanentFailure`. Malformed caller input throws
  `ArgumentException`. Notification retries transient results and disables the persisted device row
  after `TokenDead`.
- Never log tokens, credential JSON, message bodies, or file contents.
- `Firebase:ServiceAccountJson` (`Firebase__ServiceAccountJson`) accepts raw JSON or a path. Relative paths resolve from the repository
  root beside `.env`, not the process working directory.

There is deliberately no model, controller, DbContext, migration, model-assembly registration, or
HTTP file in this service. The service-account JSON belongs at the gitignored `.key/` or `.keys/`
path named by `Firebase:ServiceAccountJson`; `google-services.json` is client configuration and stays
under `Apps/Mobile/Platforms/Android/`.

## Naming decision

Firebase is named for the vendor because FCM has no independent protocol implementation underneath
it. A generic `Push` service would add an abstraction over one transport without buying a second
implementation. Email stays named for the wire protocol because its Resend transport can be swapped
for another provider behind the existing `Services/Email` boundary. Do not split or rename either
service until a real second provider justifies the change.
