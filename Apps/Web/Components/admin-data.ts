import type { IconName } from './icons';
import type { UserSort } from '@/src/api/types';

// Sort fields the API accepts for the resources whose list endpoint is sortable.
export type ResourceSort = UserSort;

export type ResourceKey =
  | 'users'
  | 'blog'
  | 'inbox'
  | 'faqs';

export type StatusTone = 'success' | 'warning' | 'danger' | 'neutral';
export type EditorValue = string | boolean;

export type ResourceConfig = {
  key: ResourceKey;
  label: string;
  singularLabel: string;
  icon: IconName;
  description: string;
  columns: [string, string, string, string];
  canCreate: boolean;
  canEdit: boolean;
  /** Only where the API lets a platform administrator delete, so the button never offers a 403. */
  canDelete: boolean;
  /** Omitted when the API exposes no sort parameter for the resource, which hides the sort control. */
  sorts?: readonly ResourceSort[];
  /**
   * The role permissions the API checks for each action. `view` passes on any one of its entries;
   * an action with no entry here is never offered.
   */
  permissions: { view: readonly string[]; create?: string; edit?: string; delete?: string };
};

export type AdminRecord = {
  id: string;
  title: string;
  subtitle: string;
  status: string;
  statusTone: StatusTone;
  meta: string;
  source?: string;
  updated: string;
  fields: { label: string; value: string; href?: string }[];
  formValues: Record<string, EditorValue>;
};

export type AdminResource = ResourceConfig & {
  records: readonly AdminRecord[];
  total: number;
  page: number;
  totalPages: number;
};

export type DashboardStat = {
  label: string;
  value: string;
  change: string;
  icon: IconName;
  tone: string;
  href: string;
};

export type RecentActivityItem = {
  initials: string;
  tone: string;
  text: string;
  time: string;
};

export type DashboardData = {
  stats: DashboardStat[];
  recentActivity: RecentActivityItem[];
};

export const resources: Record<ResourceKey, ResourceConfig> = {
  users: {
    key: 'users',
    label: 'Users',
    singularLabel: 'User',
    icon: 'users',
    description: 'Keep account access, roles, and profile details in one place.',
    columns: ['User', 'Email', 'Role', 'Updated'],
    canCreate: true,
    canEdit: true,
    canDelete: true,
    sorts: ['CreatedAt', 'Email', 'Name', 'Role'] satisfies readonly UserSort[],
    permissions: { view: ['users.read_private'], create: 'users.manage', edit: 'users.manage', delete: 'users.manage' },
  },
  blog: {
    key: 'blog',
    label: 'Blog',
    singularLabel: 'Blog post',
    icon: 'blog',
    description: 'Plan, draft, and publish company news and updates.',
    columns: ['Post', 'Author', 'Status', 'Updated'],
    canCreate: true,
    canEdit: true,
    canDelete: true,
    permissions: { view: ['list_blog_posts'], create: 'create_blog_post', edit: 'update_blog_post', delete: 'delete_blog_post' },
  },
  inbox: {
    key: 'inbox',
    label: 'Inbox',
    singularLabel: 'Conversation',
    icon: 'messages',
    description: 'Visitor messages, GitHub issues and closed conversations in one place.',
    columns: ['Conversation', 'Source', 'Status', 'Updated'],
    canCreate: false,
    canEdit: false,
    canDelete: false,
    permissions: { view: ['inbox.manage'] },
  },
  faqs: {
    key: 'faqs',
    label: "FAQ's",
    singularLabel: 'FAQ entry',
    icon: 'faq',
    description: 'Answer common questions with clear, maintained help content.',
    columns: ['Question', 'Category', 'Status', 'Updated'],
    canCreate: true,
    canEdit: true,
    canDelete: true,
    permissions: { view: ['list_faq_entries'], create: 'create_faq_entry', edit: 'update_faq_entry', delete: 'delete_faq_entry' },
  },
};

export const resourceKeys = Object.keys(resources) as ResourceKey[];

export function getResource(key: string): ResourceConfig | undefined {
  return resourceKeys.includes(key as ResourceKey) ? resources[key as ResourceKey] : undefined;
}

/**
 * The resource as one role may use it: undefined when the role cannot open it at all, and each
 * action switched off when the role lacks its permission. The API enforces the same rules; this
 * only keeps the console from offering what would come back 403.
 */
export function resourceFor(key: string, permissions: readonly string[]): ResourceConfig | undefined {
  const resource = getResource(key);
  if (!resource || !resource.permissions.view.some((permission) => permissions.includes(permission))) return undefined;
  const allowed = (permission?: string) => permission !== undefined && permissions.includes(permission);
  return {
    ...resource,
    canCreate: resource.canCreate && allowed(resource.permissions.create),
    canEdit: resource.canEdit && allowed(resource.permissions.edit),
    canDelete: resource.canDelete && allowed(resource.permissions.delete),
  };
}

export function getStatusTone(status: string): StatusTone {
  const normalized = status.toLowerCase();
  if (['published', 'open', 'admin', 'active', 'unread'].includes(normalized)) {
    return 'success';
  }
  if (['draft', 'pending', 'review', 'flagged'].includes(normalized)) {
    return 'warning';
  }
  if (['cancelled', 'expired', 'removed'].includes(normalized)) {
    return 'danger';
  }
  return 'neutral';
}

/** Dashboard shortcuts, each shown only to a role holding its permission. */
export const quickLinks = [
  { label: 'Add a user', href: '/admin/users/new', icon: 'plus' as IconName, permission: 'users.manage' },
  { label: 'Write a blog post', href: '/admin/blog/new', icon: 'blog' as IconName, permission: 'create_blog_post' },
  { label: 'Review inbox', href: '/admin/inbox', icon: 'messages' as IconName, permission: 'inbox.manage' },
  { label: 'Update company profile', href: '/admin/settings/company', icon: 'settings' as IconName, permission: 'company.manage' },
];
