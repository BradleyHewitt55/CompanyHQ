# Media service

`Services/Media` owns image/video metadata, access rules, object-storage ticket lifecycle, and cleanup.
It uses the provider-neutral `IMediaObjectStorageProvider`; Cloudflare R2 is composed by the API host.

## Rules

- `POST /api/media` issues a ticket for declared sanitized bytes. The client uploads the bytes, then calls the completion endpoint; Media verifies the object metadata before marking it ready.
- **There are two ways to upload those bytes and both end at the same `complete` call.** A native client `PUT`s the ticket's short-lived object-storage URL directly. A browser `PUT`s `/api/media/{id}/upload` instead, because a cross-origin `PUT` to a presigned URL only works if the bucket carries a CORS rule naming every origin the app is ever served from. `UploadAsync` re-checks owner, `Pending` status, declared content type and declared length before it signs anything.
- Mobile re-encodes picked images before the upload so EXIF/IPTC/XMP metadata never leaves the device. Keys are generated as `{scope}/{owner-guid}/{media-guid}.{extension}`.
- Message reads are policy-gated short-lived R2 `GET` URLs. User images use a durable URL: the configured custom domain when `Media:PublicBaseUrl` is set, otherwise `GET /api/media/{id}/content` on this API.
- `/content` is anonymous and User-scope only — the same audience a public bucket domain would serve. It answers `302` to a signed R2 URL and carries **no bytes**, so it is a signing step, not the byte proxy that stays deliberately unbuilt. A Message object is `404` there.
- Profile image URLs validate as HTTPS **or loopback HTTP** (`UserProfileValidation`). Local development serves media from the API over plain HTTP, and an HTTPS-only rule rejects every photo save with a transport-security message that has nothing to do with the photo.
- **`Media:PublicBaseUrl` must never be the R2 S3 API host.** `https://<account>.r2.cloudflarestorage.com` signs every `GET`, so an `img` gets `400 InvalidArgument`. Registration ignores that value and falls back to `/content` rather than minting URLs that can never load.
- A consumer that stores a durable URL must keep the exact URL returned by the ticket; never rebuild it with `GetPublicUrl`.
- `MediaStatus.Pending`, `MediaStatus.Failed`, and `CleanupFailureCount` must remain until production has no historical rows in those states and a Phase 5 migration is approved.
- Videos use the same owner-bound Media ticket, upload, completion, and cleanup flow as images.
- Use the var from `theme.css` instead of hard coding styles
- **An image is never downloaded, buffered, or turned into a base64 data URL before it is shown.** An upload returns a durable media URL and that URL is what gets rendered. A surface that needs a freshly picked photo on screen uploads it first and paints what came back. The single exception is the composer's own camera-roll grid: a photo still only on the phone has no URL to render, so `IDeviceGallery` inlines a small thumbnail. Anything that already has a durable URL still renders that URL.
- Do not persist upload URLs or Message download URLs: both expire, so store only the durable URL from the ticket.
- Durable content URLs are baked at ticket time: changing ContentBaseUrl invalidates stored rows, so prefer binding a real custom domain for new uploads.
- Abandoned uploads split ownership: the client DELETEs pre-save orphans, post-save replacement retires through ProfileMediaLifecycle by marking Deleted, and provider deletes run async in MediaCleanupService with transient retry and Failed quarantine.

## Boundaries

Media owns provider-neutral lifecycle and access rules. `Services/Cloudflare` supplies the R2 adapter;
`MediaService` takes `IMediaObjectStorageProvider` in its constructor, so a host without
`AddCloudflare` fails its build-time validation instead of the first upload. Media also supplies
User's `IProfileMediaLifecycle` (User is foundation, so Media may reference it). User profile photos
store the durable User-scope URL on the User record.

Media never references a feature that stores media. The `Message` scope's read policy is an
`IMediaAccessPolicy` bridged in `Apps/Api/Integrations`; a scope with no policy is unreadable. There is
no administrator shortcut: whether an operator may read a private object is the owning feature's
rule, never Media's. `MediaScope` is still a closed enum, so a new consumer scope is an edit here.

## Deliberately not built

There is no R2 Worker, API image-byte **read** proxy (the `/content` redirect never touches the bytes; `PUT /api/media/{id}/upload` is the write side and deliberately does), local filesystem adapter, public bucket outside `user/`, arbitrary remote-media URL, thumbnail pipeline, or media administration UI.

