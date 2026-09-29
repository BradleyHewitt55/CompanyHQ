import 'server-only';

import {
  getResource,
  getStatusTone,
  type AdminRecord,
  type AdminResource,
  type DashboardData,
  type RecentActivityItem,
  type ResourceKey,
  type ResourceSort,
} from '../../Components/admin-data';
import { requestJson } from './client';
import type {
  AdminEmailTemplateDto,
  ClientBlogPostDto,
  AdminConversationDto,
  ClientFaqEntryDto,
  ClientUserDto,
  MessagePageDto,
  PagedResultOfClientBlogPostDto,
  PagedResultOfClientFaqEntryDto,
  PagedResultOfAdminConversationDto,
  PagedResultOfClientUserDto,
  SortDirection,
} from './types';
import { fullName } from '@/lib/utils';

const pageSize = 10;

/** Narrowing and ordering the API does in SQL. The list UI never filters a loaded page itself. */
export type AdminResourceQuery = {
  search?: string;
  sort?: ResourceSort;
  direction?: SortDirection;
};

export function getEmailTemplates() {
  return requestJson<AdminEmailTemplateDto[]>('/api/email-templates');
}

export async function getAdminResource(
  resourceKey: string,
  page = 1,
  query: AdminResourceQuery = {},
): Promise<AdminResource | undefined> {
  const config = getResource(resourceKey);
  if (!config) return undefined;

  const result = await listRecords(config.key, page, query);
  return { ...config, ...result };
}

export async function getAdminRecord(resourceKey: string, recordId: string): Promise<AdminRecord | undefined> {
  const config = getResource(resourceKey);
  if (!config) return undefined;

  switch (config.key) {
    case 'users':
    {
      const user = await requestJson<ClientUserDto>(`/api/users/${encodeURIComponent(recordId)}`);
      return mapUser(user);
    }
    case 'blog':
      return mapBlog(await requestJson<ClientBlogPostDto>(`/api/blog/${encodeURIComponent(recordId)}`));
    case 'messages': {
      const conversation = await findClosedConversation(recordId);
      if (!conversation) return undefined;

      const record = mapConversation(conversation);
      const messagePages: MessagePageDto[] = [];
      let cursor: string | null | undefined;
      do {
        const query = cursor ? `?cursor=${encodeURIComponent(cursor)}` : '';
        const messagePage = await requestJson<MessagePageDto>(
          `/api/conversations/admin/closed/${encodeURIComponent(recordId)}/messages${query}`,
        );
        messagePages.push(messagePage);
        cursor = messagePage.nextCursor;
      } while (cursor);
      const messages = messagePages.flatMap((page) => page.messages);
      return record
        ? {
            ...record,
            fields: [
              ...record.fields,
              ...messages.map((message) => ({
                label: formatDate(message.createdAt),
                value: message.body,
              })),
            ],
          }
        : undefined;
    }
    case 'faqs':
      return mapFaq(await requestJson<ClientFaqEntryDto>(`/api/faqs/${encodeURIComponent(recordId)}`));
  }
}

async function findClosedConversation(recordId: string): Promise<AdminConversationDto | undefined> {
  let page = 1;
  while (true) {
    const result = await requestJson<PagedResultOfAdminConversationDto>(
      `/api/conversations/admin/closed?${pageQuery(page)}`,
    );
    const conversation = result.items.find((item) => item.id === recordId);
    if (conversation || !result.hasNext) return conversation;
    page += 1;
  }
}

export async function getDashboardData(): Promise<DashboardData> {
  const query = pageQuery();
  const [users, blogPosts, faqs] = await Promise.all([
    requestJson<PagedResultOfClientUserDto>(`/api/users?${query}`),
    requestJson<PagedResultOfClientBlogPostDto>(`/api/blog?${query}`),
    requestJson<PagedResultOfClientFaqEntryDto>(`/api/faqs?${query}`),
  ]);

  const activity = [
    ...users.items.map((user) => {
      const name = userLabel(user);
      return activityItem(name, `${name} account is in the live user directory.`, userDate(user), 'teal');
    }),
    ...blogPosts.items.map((post) =>
      activityItem(post.title, `${post.title} is on the blog.`, contentDate(post), 'mint'),
    ),
    ...faqs.items.map((faq) =>
      activityItem(faq.question, `${faq.question} is in the FAQ.`, contentDate(faq), 'rose'),
    ),
  ]
    .sort((left, right) => right.timestamp.localeCompare(left.timestamp))
    .slice(0, 4)
    .map(({ timestamp: _timestamp, ...item }) => item);

  return {
    stats: [
      dashboardStat('Registered users', users.total, '/admin/users', 'users', 'teal'),
      dashboardStat('Blog posts', blogPosts.total, '/admin/blog', 'blog', 'mint'),
      dashboardStat("FAQ's", faqs.total, '/admin/faqs', 'faq', 'rose'),
    ],
    recentActivity: activity,
  };
}

async function listRecords(
  resourceKey: ResourceKey,
  page: number,
  query: AdminResourceQuery,
): Promise<AdminResourceList> {
  switch (resourceKey) {
    case 'users': {
      const result = await requestJson<PagedResultOfClientUserDto>(`/api/users?${pageQuery(page, query, true)}`);
      return pagedList(result.items.map(mapUser).filter(isRecord), result.total, page);
    }
    case 'blog': {
      const result = await requestJson<PagedResultOfClientBlogPostDto>(`/api/blog?${pageQuery(page, query)}`);
      return pagedList(result.items.map(mapBlog).filter(isRecord), result.total, page);
    }
    case 'messages': {
      const result = await requestJson<PagedResultOfAdminConversationDto>(
        `/api/conversations/admin/closed?${pageQuery(page, query)}`,
      );
      return pagedList(result.items.map(mapConversation).filter(isRecord), result.total, page);
    }
    case 'faqs': {
      const result = await requestJson<PagedResultOfClientFaqEntryDto>(`/api/faqs?${pageQuery(page, query)}`);
      return pagedList(result.items.map(mapFaq).filter(isRecord), result.total, page);
    }
  }
}

function pageQuery(page = 1, query: AdminResourceQuery = {}, sortable = false) {
  const params = new URLSearchParams({ Page: String(page), PageSize: String(pageSize) });
  const search = query.search?.trim();
  if (search) params.set('search', search);
  if (sortable && query.sort) {
    params.set('sort', query.sort);
    params.set('direction', query.direction ?? 'Descending');
  }

  return params.toString();
}

function pagedTotal(total: number | string, fallback: number) {
  const value = Number(total);
  return Number.isFinite(value) ? value : fallback;
}

type AdminResourceList = Pick<AdminResource, 'records' | 'total' | 'page' | 'totalPages'>;

function pagedList(records: AdminRecord[], totalValue: number | string, page: number): AdminResourceList {
  const total = pagedTotal(totalValue, records.length);
  return {
    records,
    total,
    page,
    totalPages: Math.max(1, Math.ceil(total / pageSize)),
  };
}

function mapUser(user: ClientUserDto): AdminRecord | undefined {
  if (!('id' in user)) return undefined;

  const adminUser = 'email' in user ? user : undefined;
  const title = fullName(user) || adminUser?.email || `User ${user.id.slice(0, 8)}`;
  const role = adminUser?.roleName
    || adminUser?.role || 'Profile';

  return {
    id: user.id,
    title,
    subtitle: adminUser?.email || 'Email is not exposed by this API audience.',
    status: role,
    statusTone: getStatusTone(role),
    meta: adminUser?.email || 'Profile',
    updated: formatDate(adminUser?.updatedAt || user.createdAt),
    fields: [
      { label: 'Email', value: adminUser?.email || 'Not exposed' },
      { label: 'Role', value: role },
      { label: 'MCP access', value: adminUser?.mcpAllowedTools?.join(', ') || 'None' },
      { label: 'Bio', value: user.bio || 'No bio provided' },
      { label: 'Joined', value: formatDate(user.createdAt) },
    ],
    formValues: {
      firstName: user.firstName || '',
      lastName: user.lastName || '',
      email: adminUser?.email || '',
      role: adminUser?.role || 'Client',
      roleName: adminUser?.roleName || adminUser?.role || 'Client',
      bio: user.bio || '',
      mcpDisabledTools: JSON.stringify(adminUser?.mcpDisabledTools || []),
      mcpAllowedTools: JSON.stringify(adminUser?.mcpAllowedTools || []),
    },
  };
}

function mapBlog(post: ClientBlogPostDto): AdminRecord {
  const adminPost = 'isDraft' in post ? post : undefined;
  const status = adminPost?.isDraft ? 'Draft' : 'Published';

  return {
    id: post.slug,
    title: post.title,
    subtitle: post.description,
    status,
    statusTone: getStatusTone(status),
    meta: post.author,
    updated: formatDate(adminPost?.updatedAt || post.createdAt),
    fields: [
      { label: 'Slug', value: post.slug },
      { label: 'Author', value: post.author },
      { label: 'Category', value: post.category },
      { label: 'Description', value: post.description },
      { label: 'Body', value: post.body },
      { label: 'Published', value: formatDate(post.publishedAt) },
    ],
    formValues: {
      slug: post.slug,
      title: post.title,
      description: post.description,
      body: post.body,
      author: post.author,
      category: post.category,
      coverImageUrl: post.coverImageUrl || '',
      publishedAt: formatDateTimeLocal(post.publishedAt),
      sortOrder: String(post.sortOrder),
      isDraft: adminPost?.isDraft ?? false,
    },
  };
}

function mapConversation(conversation: AdminConversationDto): AdminRecord {
  const participants = conversation.participants
    .map((participant) => fullName(participant) || `User ${participant.userId.slice(0, 8)}`)
    .join(' / ');
  const status = 'Closed';

  return {
    id: conversation.id,
    title: participants || 'Closed conversation',
    subtitle: conversation.lastMessagePreview || 'No messages',
    status,
    statusTone: getStatusTone(status),
    meta: `${conversation.messageCount} message${conversation.messageCount === 1 ? '' : 's'}`,
    updated: formatDate(conversation.lastMessageAt),
    fields: [
      { label: 'Participants', value: participants || 'Unknown' },
      { label: 'Messages', value: String(conversation.messageCount) },
      { label: 'Last message', value: conversation.lastMessagePreview || 'No messages yet' },
      { label: 'Closed', value: formatDate(conversation.closedAt) },
      { label: 'Conversation', value: conversation.id },
    ],
    formValues: {},
  };
}

function mapFaq(faq: ClientFaqEntryDto): AdminRecord {
  const adminFaq = 'isDraft' in faq ? faq : undefined;
  const status = adminFaq?.isDraft ? 'Draft' : 'Published';

  return {
    id: faq.key,
    title: faq.question,
    subtitle: faq.answer,
    status,
    statusTone: getStatusTone(status),
    meta: faq.category,
    updated: formatDate(adminFaq?.updatedAt || faq.createdAt),
    fields: [
      { label: 'Key', value: faq.key },
      { label: 'Question', value: faq.question },
      { label: 'Answer', value: faq.answer },
      { label: 'Category', value: faq.category },
      { label: 'SEO description', value: faq.seoDescription },
      { label: 'Published', value: formatDate(faq.publishedAt) },
    ],
    formValues: {
      key: faq.key,
      question: faq.question,
      answer: faq.answer,
      category: faq.category,
      seoDescription: faq.seoDescription,
      publishedAt: formatDateTimeLocal(faq.publishedAt),
      sortOrder: String(faq.sortOrder),
      isDraft: adminFaq?.isDraft ?? false,
    },
  };
}

function dashboardStat(
  label: string,
  value: number | string,
  href: string,
  icon: DashboardData['stats'][number]['icon'],
  tone: string,
) {
  return { label, value: String(value), change: 'Live from API', href, icon, tone };
}

function activityItem(
  subject: string,
  text: string,
  timestamp: string,
  tone: string,
): RecentActivityItem & { timestamp: string } {
  return {
    initials: initials(subject),
    tone,
    text,
    time: formatDate(timestamp),
    timestamp,
  };
}

function userLabel(user: ClientUserDto) {
  return fullName(user) || ('email' in user ? user.email : 'User');
}

function userDate(user: ClientUserDto) {
  return 'updatedAt' in user ? user.updatedAt : user.createdAt;
}

function contentDate(item: { createdAt: string } | { createdAt: string; updatedAt: string }) {
  return 'updatedAt' in item ? item.updatedAt : item.createdAt;
}


function formatDate(value: string | null | undefined) {
  if (!value) return 'Not recorded';

  const date = new Date(value);
  return Number.isNaN(date.valueOf())
    ? 'Not recorded'
    : new Intl.DateTimeFormat('en', { dateStyle: 'medium' }).format(date);
}

function formatDateTimeLocal(value: string | null | undefined) {
  if (!value) return '';

  const date = new Date(value);
  return Number.isNaN(date.valueOf()) ? '' : date.toISOString().slice(0, 16);
}

function initials(value: string) {
  const words = value.trim().split(/\s+/).filter(Boolean);
  return words
    .slice(0, 2)
    .map((word) => word[0]?.toUpperCase() || '')
    .join('') || 'PG';
}

function isRecord(record: AdminRecord | undefined): record is AdminRecord {
  return record !== undefined;
}
