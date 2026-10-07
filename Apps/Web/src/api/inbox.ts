/* An inbox thread is addressed by its channel and the channel's own id. The console carries both in
   one record id so the generic list can link to it. Channel keys never contain a dot. */

export function inboxRecordId(channel: string, id: string) {
  return `${channel}.${id}`;
}

export function parseInboxRecordId(recordId: string): { channel: string; id: string } | undefined {
  const split = recordId.indexOf('.');
  return split > 0 && split < recordId.length - 1
    ? { channel: recordId.slice(0, split), id: recordId.slice(split + 1) }
    : undefined;
}

export function inboxThreadPath(channel: string, id: string) {
  return `/api/inbox/${encodeURIComponent(channel)}/${encodeURIComponent(id)}`;
}
