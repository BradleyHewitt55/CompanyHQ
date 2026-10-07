import 'server-only';

import {
  getStatusTone,
  resourceFor,
  type AdminRecord,
  type AdminResource,
  type DashboardData,
  type RecentActivityItem,
  type ResourceKey,
  type ResourceSort,
} from '../../Components/admin-data';
import { requestJson } from './client';
import { inboxRecordId } from './inbox';
import type {
  AdminEmailTemplateDto,
  ClientBlogPostDto,
  AdminInboxChannelDto,
  AdminInboxItemDto,
  ClientFaqEntryDto,
  ClientUserDto,
  PagedResultOfClientBlogPostDto,
  PagedResultOfClientFaqEntryDto,
  PagedResultOfAdminInboxItemDto,
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

/** Undefined when the resource does not exist or the role may not open it. */
export async function getAdminResource(
  resourceKey: string,
  permissions: readonly string[],
  page = 1,
  query: AdminResourceQuery = {},
): Promise<AdminResource | undefined> {
  const config = resourceFor(resourceKey, permissions);
  if (!config) return undefined;

  const result = await listRecords(config.key, permissions, page, query);
  return { ...config, ...result };
}

export async function getAdminRecord(
  resourceKey: string,
  permissions: readonly string[],
  recordId: string,
): Promise<AdminRecord | undefined> {
  const config = resourceFor(resourceKey, permissions);
  if (!config) return undefined;

  switch (config.key) {
    case 'users':
    {
      const user = await requestJson<ClientUserDto>(`/api/users/${encodeURIComponent(recordId)}`);
      return mapUser(user);
    }
    case 'blog':
      return mapBlog(await requestJson<ClientBlogPostDto>(`/api/blog/${encodeURIComponent(recordId)}`));
    // An inbox thread has its own page; it is never read through the generic record view.
    case 'inbox':
      return undefined;
    case 'faqs':
      return mapFaq(await requestJson<ClientFaqEntryDto>(`/api/faqs/${encodeURIComponent(recordId)}`));
  }
}

/** Counts and recent activity for the lists this role may open; the rest are simply absent. */
export async function getDashboardData(permissions: readonly string[]): Promise<DashboardData> {
  const query = pageQuery();
  const when = <T,>(resource: ResourceKey, load: () => Promise<T>) =>
    resourceFor(resource, permissions) ? load() : Promise.resolve(undefined);
  const [users, blogPosts, faqs] = await Promise.all([
    when('users', () => requestJson<PagedResultOfClientUserDto>(`/api/users?${query}`)),
    when('blog', () => requestJson<PagedResultOfClientBlogPostDto>(`/api/blog?${query}`)),
    when('faqs', () => requestJson<PagedResultOfClientFaqEntryDto>(`/api/faqs?${query}`)),
  ]);

  const activity = [
    ...(users?.items ?? []).map((user) => {
      const name = userLabel(user);
      return activityItem(name, `${name} account is in the live user directory.`, userDate(user), 'teal');
    }),
    ...(blogPosts?.items ?? []).map((post) =>
      activityItem(post.title, `${post.title} is on the blog.`, contentDate(post), 'mint'),
    ),
    ...(faqs?.items ?? []).map((faq) =>
      activityItem(faq.question, `${faq.question} is in the FAQ.`, contentDate(faq), 'rose'),
    ),
  ]
    .sort((left, right) => right.timestamp.localeCompare(left.timestamp))
    .slice(0, 4)
    .map(({ timestamp: _timestamp, ...item }) => item);

  return {
    stats: [
      users && dashboardStat('Registered users', users.total, '/admin/users', 'users', 'teal'),
      blogPosts && dashboardStat('Blog posts', blogPosts.total, '/admin/blog', 'blog', 'mint'),
      faqs && dashboardStat("FAQ's", faqs.total, '/admin/faqs', 'faq', 'rose'),
    ].filter((stat) => stat !== undefined),
    recentActivity: activity,
  };
}

async function listRecords(
  resourceKey: ResourceKey,
  permissions: readonly string[],
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
    case 'inbox':
      return listInbox(page, query);
    case 'faqs': {
      const result = await requestJson<PagedResultOfClientFaqEntryDto>(`/api/faqs?${pageQuery(page, query)}`);
      return pagedList(result.items.map(mapFaq).filter(isRecord), result.total, page);
    }
  }
}

// Every channel this role may open, one after the other, paged as one list. A channel that is not
// connected, or whose backend fails, contributes nothing rather than breaking the inbox.
async function listInbox(page: number, query: AdminResourceQuery): Promise<AdminResourceList> {
  const channels = (await requestJson<AdminInboxChannelDto[]>('/api/inbox/channels')).filter((channel) => channel.available);
  const load = (channel: string, channelPage: number) =>
    requestJson<PagedResultOfAdminInboxItemDto>(`/api/inbox/${encodeURIComponent(channel)}?${pageQuery(channelPage, query)}`)
      .catch(() => ({ items: [] as AdminInboxItemDto[], total: 0 }));
  const firstPages = await Promise.all(channels.map((channel) => load(channel.key, 1)));
  const records: AdminRecord[] = [];
  let skip = (page - 1) * pageSize;

  for (const [index, channel] of channels.entries()) {
    const total = Number(firstPages[index].total);
    let offset = Math.min(skip, total);
    skip -= offset;
    while (records.length < pageSize && offset < total) {
      const channelPage = Math.floor(offset / pageSize) + 1;
      const result = channelPage === 1 ? firstPages[index] : await load(channel.key, channelPage);
      const items = result.items.slice(offset % pageSize, offset % pageSize + pageSize - records.length);
      if (!items.length) break;
      records.push(...items.map((item) => mapInboxItem(item, channel.name)));
      offset += items.length;
    }
  }

  return pagedList(records, firstPages.reduce((sum, result) => sum + Number(result.total), 0), page);
}

function mapInboxItem(item: AdminInboxItemDto, channelName: string): AdminRecord {
  const status = item.open ? 'Open' : 'Closed';
  return {
    id: inboxRecordId(item.channel, item.id),
    title: item.title,
    subtitle: item.preview,
    status,
    statusTone: getStatusTone(status),
    meta: item.contact || item.author,
    source: item.source ? `${channelName} · ${item.source}` : channelName,
    updated: formatDate(item.lastActivityAt),
    fields: [],
    formValues: {},
  };
}

function pageQuery(page = 1, query: AdminResourceQuery = {}, sortable = false) {
  const params = new URLSearchParams({ Page: String(page), PageSize: String(pageSize) });
  const search = query.search?.trim();
  if (search) params.set('keyword', search);
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
      coverMediaObjectId: post.coverMediaObjectId ?? '',
      author: post.author,
      category: post.category,
      coverImageUrl: post.coverImageUrl || '',
      publishedAt: formatDateTimeLocal(post.publishedAt),
      sortOrder: String(post.sortOrder),
      isDraft: adminPost?.isDraft ?? false,
    },
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
