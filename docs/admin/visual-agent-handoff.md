# Visual agent handoff

Owners and developers can turn an admin, website, mobile-app, or desktop-app visual issue into a precisely scoped coding task without exposing supplied base-admin source.

## Inspect mode

In an authorized preview, Inspect mode adds a stable opaque component reference to each editable surface. Hovering shows a visible outline and breadcrumb. Clicking pins the component and opens its details.

The inspector shows:

- page reference, route, and list or detail variant;
- stable component reference and accessible label;
- effective layer and source revision;
- DTO binding and safe field schema;
- viewport, bounding box, text/style snapshot, and selected CSS classes as diagnostics;
- tenant source path and symbol only when the selected component belongs to tenant-owned override code.

CSS classes help a user point out a visual error, but they are not the authority for an edit. Component references are stable; classes collide and change.

## Comment and coding composer

After pinning a component, the owner/developer can attach a comment and choose an approved coding agent. A chat composer docks at the bottom of the preview. It can be minimized and restored without losing the selected component, draft, or task status.

Submitting creates a UiEditIntent and a Pipeline task. The agent receives only:

- the delegating user's effective permissions;
- page, component, task, Change, service/project, and worktree scope;
- sanitized comment, safe screenshot, viewport/selection metadata, and allowed file list;
- DTO schema, authorized bindings, and task-relevant DTO data;
- tenant-owned source only when that source exists and is in scope.

The agent does not receive supplied base source, source maps, hidden internal class names, raw models, secrets, another task's files, or unrestricted repository credentials.

## Sparse override behavior

The Page Catalog renders supplied admin pages by name and data contract even when no tenant code exists. The Source Explorer shows only tenant-created admin files. Project previews use the selected project's protected main repository by default and expose only that project's authorized source.

Choosing Create override or sending a base component to an agent creates a Change worktree and the first same-name override files in the company root repository. The agent builds from the page/component/DTO contract, not a copied base implementation. Deleting the final override through a Change restores the supplied UI immediately.

For a website/mobile/desktop project, the same interaction creates a Change worktree in that project's repository. The selected component, comments, safe DTO/API contract, and permitted project files are passed to the coding agent; protected main is never edited directly.

## Preview transport safety

The preview bridge validates its source window, expected origin, session, nonce, page revision, component reference, and message schema. It never accepts wildcard preview messages. Comments, DOM text, classes, styles, screenshots, and external content are untrusted data and cannot grant instructions or authority.

## Completion evidence

A visual handoff Change includes its selection record, comment, task, agent identity, permitted files, DTO contract, diff, preview, UI tests, policy checks, and pull request/approval outcome. See [testing](../testing.md) and [Explorer overrides](explorer-overrides.md).
