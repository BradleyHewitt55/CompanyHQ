'use client';

import { useEffect, useRef, useState, type FormEvent } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { Icon } from '@/Components/icons';
import { Button } from '@/Components/ui/button';
import { Textarea } from '@/Components/ui/textarea';
import { deleteInboxThreadAction, inboxThreadAction } from '@/app/admin/actions';
import type { AdminInboxThreadDto } from '@/src/api/types';
import { useAdminBreadcrumbDetail } from '@/app/admin/breadcrumb';

const stamp = (value: string) => new Date(value).toLocaleString('en-ZA', { dateStyle: 'medium', timeStyle: 'short' });

// Only the channel Inbox stores itself is cheap enough to re-read every few seconds.
const liveChannel = 'messages';

function Entry({ author, at, body, reply = false }: { author: string; at: string; body: string; reply?: boolean }) {
  return <article className={'admin-message-thread__entry' + (reply ? ' admin-message-thread__entry--reply' : '')}>
    <p>{body}</p>
    <footer>{author} · <time dateTime={at}>{stamp(at)}</time></footer>
  </article>;
}

export function MessageThread({ initial }: { initial: AdminInboxThreadDto }) {
  const [thread, setThread] = useState(initial);
  const [body, setBody] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const history = useRef<HTMLDivElement>(null);
  const router = useRouter();
  const { item } = thread;
  useAdminBreadcrumbDetail(item.title || 'Conversation');

  // Chats read from the bottom, so a new message (including the 5 second refresh) brings the latest into view.
  useEffect(() => { history.current?.scrollTo({ top: history.current.scrollHeight }); }, [thread.messages.length]);

  useEffect(() => {
    if (item.channel !== liveChannel) return;
    const refresh = async () => {
      if (document.visibilityState !== 'visible') return;
      try {
        const result = await inboxThreadAction(item.channel, item.id);
        if (result.thread) setThread(result.thread);
      } catch { /* Keep the current conversation visible. */ }
    };
    const timer = window.setInterval(() => void refresh(), 5000);
    return () => window.clearInterval(timer);
  }, [item.channel, item.id]);

  async function change(request: { reply: string } | { resolve: true }) {
    setBusy(true);
    setError('');
    try {
      const result = await inboxThreadAction(item.channel, item.id, request);
      if (result.thread) setThread(result.thread);
      setError(result.error);
      if (!result.error && 'reply' in request) setBody('');
    } catch (cause) { setError((cause as Error).message); }
    finally { setBusy(false); }
  }

  function reply(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (body.trim()) void change({ reply: body });
  }

  async function remove() {
    if (!window.confirm('Delete this conversation and all its messages for everyone? This cannot be undone.')) return;
    setBusy(true);
    setError('');
    try {
      const result = await deleteInboxThreadAction(item.channel, item.id);
      if (result.error) setError(result.error);
      else router.replace('/admin/inbox');
    } catch (cause) { setError((cause as Error).message); }
    finally { setBusy(false); }
  }

  return <section aria-label="Conversation" className="admin-message-thread">
    <header className="admin-message-thread__header">
      <Link aria-label="Back to inbox" className="admin-message-thread__back" href="/admin/inbox"><Icon name="arrow-left" size={20} /></Link>
      <span aria-hidden="true" className="admin-message-thread__avatar">{(item.title || '?').charAt(0).toUpperCase()}</span>
      <div className="admin-message-thread__who">
        <h1 className="admin-message-thread__title">{item.title || 'Conversation'}</h1>
        {item.contact && <a href={`mailto:${item.contact}`}>{item.contact}</a>}
        {item.url && <a href={item.url} rel="noreferrer" target="_blank">Open on {item.source}</a>}
      </div>
      <span className={'admin-message-thread__badge' + (item.open ? ' admin-message-thread__badge--open' : '')}>
        {[item.source, item.open ? 'Open' : 'Closed'].filter(Boolean).join(' · ')}
      </span>
      {thread.canResolve && <Button disabled={busy} onClick={() => void change({ resolve: true })} type="button" variant="outline">Resolve</Button>}
      {thread.canDelete && <Button disabled={busy} onClick={() => void remove()} type="button" variant="destructive">Delete</Button>}
    </header>

    <div className="admin-message-thread__history" ref={history}>
      <div className="admin-message-thread__stream">
        {item.origin && <p className="admin-message-thread__retention">Sent from {item.origin}</p>}
        {thread.messages.map(entry => <Entry at={entry.createdAt} author={entry.author} body={entry.body} key={entry.id} reply={entry.fromStaff} />)}
      </div>
    </div>

    {thread.canReply ? <form className="admin-message-thread__composer" onSubmit={reply}>
      {error && <p role="alert">{error}</p>}
      <div className="admin-message-thread__input">
        <Textarea
          aria-label="Reply"
          maxLength={4000}
          onChange={event => setBody(event.target.value)}
          onKeyDown={event => { if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); event.currentTarget.form?.requestSubmit(); } }}
          placeholder="Reply"
          required
          rows={1}
          value={body}
        />
        <Button aria-label={busy ? 'Sending reply' : 'Send reply'} disabled={busy || !body.trim()} size="icon" type="submit"><Icon name="send" size={18} /></Button>
      </div>
    </form>
      : <p className="admin-message-thread__retention" role={error ? 'alert' : undefined}>{error || 'This conversation is read only.'}</p>}
  </section>;
}
