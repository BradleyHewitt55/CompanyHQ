import './admin.css';
import './(pages)/(dashboard)/dashboard.css';
import './_resources/list.css';
import './_resources/editor.css';
import './(pages)/settings/settings.css';
import './(pages)/email-templates/email-templates.css';
import { redirect } from 'next/navigation';

import AdminShell from './shell';
import { ApiError, requestJson } from '../../src/api/client';
import { readAccessToken } from '../../src/api/session';
import { canOpenAdmin } from '../../src/api/access';
import { getCompanyProfile } from '../../src/api/company';
import type { AuthUserDto, PagedResultOfAdminConversationDto } from '../../src/api/types';

export const dynamic = 'force-dynamic';

export default function AdminLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <AdminLayoutContent>{children}</AdminLayoutContent>;
}

async function AdminLayoutContent({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  if (!await readAccessToken()) {
    redirect('/auth/login?returnTo=/admin');
  }

  let currentUser: AuthUserDto;
  try {
    currentUser = await requestJson<AuthUserDto>('/api/auth/me');
  } catch (error) {
    if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
      redirect('/auth/login?returnTo=/admin');
    }
    throw error;
  }

  if (!canOpenAdmin(currentUser.permissions)) {
    redirect('/');
  }

  let latestClosedConversation: PagedResultOfAdminConversationDto['items'][number] | null = null;
  if (currentUser.permissions.includes('conversations.manage')) {
    try {
      const closed = await requestJson<PagedResultOfAdminConversationDto>(
        '/api/conversations/admin/closed?page=1&pageSize=1',
      );
      latestClosedConversation = closed.items[0] ?? null;
    } catch (error) {
      if (!(error instanceof ApiError) || error.status >= 500) {
        throw error;
      }
    }
  }

  const company = await getCompanyProfile();
  return <AdminShell company={company} currentUser={currentUser} latestClosedConversation={latestClosedConversation}>{children}</AdminShell>;
}
