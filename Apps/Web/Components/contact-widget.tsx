'use client';

import { useActionState, useState } from 'react';
import { usePathname } from 'next/navigation';
import { submitWidget } from '../app/widget-actions';
import { Input } from '@/Components/ui/input';
import { Textarea } from '@/Components/ui/textarea';
import type { ClientWidgetDto } from '@/src/api/types';

/* The public contact form. It offers only what the API reports as available: a message to the staff
   inbox, a bug report, or both. */
export function ContactWidget({ widget }: { widget: ClientWidgetDto }) {
  const [state, action, pending] = useActionState(submitWidget, { error: '', success: '' });
  const [kind, setKind] = useState<'message' | 'issue'>(widget.messages ? 'message' : 'issue');
  const pathname = usePathname();
  const report = kind === 'issue';

  if (state.success) return <p className="site-widget__done" role="status">{state.success}</p>;

  return <form action={action} className="site-widget">
    <input name="kind" type="hidden" value={kind} />
    <input name="origin" type="hidden" value={pathname} />
    {widget.messages && widget.issues && <div className="site-widget__kinds" role="group" aria-label="What would you like to send?">
      <button aria-pressed={!report} className="site-button" onClick={() => setKind('message')} type="button">Send a message</button>
      <button aria-pressed={report} className="site-button" onClick={() => setKind('issue')} type="button">Report a problem</button>
    </div>}
    {report ? <>
      <label>Type
        <select className="site-widget__select" name="category">
          {widget.issueCategories.map(category => <option key={category}>{category}</option>)}
        </select>
      </label>
      <label>Summary<Input maxLength={160} minLength={5} name="title" required /></label>
    </> : <label>Your name<Input autoComplete="name" maxLength={120} minLength={2} name="name" required /></label>}
    <label>{report ? 'Email (optional)' : 'Email'}<Input autoComplete="email" maxLength={254} name="email" required={!report} type="email" /></label>
    <label>{report ? 'What happened?' : 'Message'}<Textarea maxLength={4000} minLength={10} name="message" required rows={4} /></label>
    {state.error && <p className="site-widget__error" role="alert">{state.error}</p>}
    <button className="site-button site-button--primary" disabled={pending} type="submit">{pending ? 'Sending…' : 'Send'}</button>
  </form>;
}
