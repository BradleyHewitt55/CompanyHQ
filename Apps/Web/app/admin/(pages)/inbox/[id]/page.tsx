import { notFound } from 'next/navigation';
import { ApiError, requestJson } from '@/src/api/client';
import { requirePermission } from '@/src/api/current-user';
import { inboxThreadPath, parseInboxRecordId } from '@/src/api/inbox';
import type { AdminInboxThreadDto } from '@/src/api/types';
import { MessageThread } from './message-thread';
import './message-thread.css';

type Props = { params: Promise<{ id: string }> };

export const metadata = { title: 'Conversation' };

export default async function Page({ params }: Props) {
  await requirePermission('inbox.manage');
  const thread = parseInboxRecordId(decodeURIComponent((await params).id));
  if (!thread) notFound();

  try {
    // A channel this role cannot open answers 403, which reads here as the thread not existing.
    return <MessageThread initial={await requestJson<AdminInboxThreadDto>(inboxThreadPath(thread.channel, thread.id))} />;
  } catch (error) {
    if (error instanceof ApiError && (error.status === 404 || error.status === 403)) notFound();
    throw error;
  }
}
