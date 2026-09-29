# Cloudflare service

`Services/Cloudflare` is the vendor boundary for Cloudflare R2. It implements Media's provider-neutral
object-storage port; Media owns the lifecycle and never sees R2 credentials, endpoints, or signing.

## Rules

- `R2Options` and `CloudflareR2ObjectStorageProvider` own R2 credentials, endpoint construction, SigV4 signing, metadata checks, and deletion transport.
- Credentials bind from the `Cloudflare:R2` section only — from the environment,
  `Cloudflare__R2__AccountId`, `__AccessKeyId`, `__SecretAccessKey`, and `__Bucket`. All four are
  required; startup throws otherwise.
- `AddCloudflare` is what fills Media's `IMediaObjectStorageProvider`. Without it the host refuses to
  start, because `MediaService` takes the port in its constructor and the host validates on build.
- R2 accepts only the image/video object operations exposed by Media's port; it does not own Media entities or lifecycle decisions.

## Deliberately not built

Cloudflare owns no entity, controller, data folder, Worker, or Media lifecycle policy.
