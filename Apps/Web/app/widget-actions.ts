'use server';

import { getApiUrl } from '../src/api/client';
import type { ApiProblem, StartWidgetConversationRequest, SubmitWidgetIssueRequest } from '../src/api/types';

export type WidgetState = { error: string; success: string };

// The source this site is registered under in the API's Widget:Sources.
const source = 'Website';

/** Sends the public contact widget's form to the API: a message to the staff inbox, or a bug report. */
export async function submitWidget(_state: WidgetState, form: FormData): Promise<WidgetState> {
  const field = (name: string) => String(form.get(name) ?? '').trim();
  const report = field('kind') === 'issue';
  const origin = field('origin') || undefined;
  const body: StartWidgetConversationRequest | SubmitWidgetIssueRequest = report
    ? { category: field('category'), title: field('title'), message: field('message'), email: field('email') || null, source, origin }
    : { name: field('name'), email: field('email'), message: field('message'), source, origin };

  try {
    const response = await fetch(getApiUrl() + (report ? '/api/widget/issues' : '/api/widget/conversations'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
      cache: 'no-store',
      signal: AbortSignal.timeout(20000),
    });
    if (!response.ok) {
      const problem: ApiProblem | null = await response.json().catch(() => null);
      return {
        error: response.status === 429 ? 'Too many messages from this address. Try again in a few minutes.'
          : response.status === 400 ? 'Check the form: a message needs at least 10 characters and a valid email.'
            : problem?.title || 'Your message could not be sent. Try again.',
        success: '',
      };
    }
  } catch { return { error: 'Your message could not be sent. Try again.', success: '' }; }

  return { error: '', success: report ? 'Thanks, your report has been filed.' : 'Thanks, we have your message and will reply by email.' };
}
