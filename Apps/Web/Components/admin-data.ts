import type { IconName } from './icons';
import type { UserSort } from '@/src/api/types';

// Sort fields the API accepts for the resources whose list endpoint is sortable.
export type ResourceSort = UserSort;

export type ResourceKey =
  | 'users'
  | 'blog'
  | 'messages'
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
  /** Omitted when the API exposes no sort parameter for the resource, which hides the sort control. */
  sorts?: readonly ResourceSort[];
};

export type AdminRecord = {
  id: string;
  title: string;
  subtitle: string;
  status: string;
  statusTone: StatusTone;
  meta: string;
  updated: string;
  fields: { label: string; value: string }[];
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
    sorts: ['CreatedAt', 'Email', 'Name', 'Role'] satisfies readonly UserSort[],
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
  },
  messages: {
    key: 'messages',
    label: 'Messages',
    singularLabel: 'Conversation',
    icon: 'messages',
    description: 'Review closed conversations and retain their history for operations.',
    columns: ['Conversation', 'Participant', 'Status', 'Updated'],
    canCreate: false,
    canEdit: false,
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
  },
};

export const resourceKeys = Object.keys(resources) as ResourceKey[];

export function getResource(key: string): ResourceConfig | undefined {
  return resourceKeys.includes(key as ResourceKey) ? resources[key as ResourceKey] : undefined;
}

export function getStatusTone(status: string): StatusTone {
  const normalized = status.toLowerCase();
  if (['published', 'open', 'admin', 'active', 'unread'].includes(normalized)) {
    return 'success';
  }
  if (['draft', 'pending', 'review'].includes(normalized)) {
    return 'warning';
  }
  if (['cancelled', 'expired'].includes(normalized)) {
    return 'danger';
  }
  return 'neutral';
}

export const quickLinks = [
  { label: 'Add a user', href: '/admin/users/new', icon: 'plus' as IconName },
  { label: 'Write a blog post', href: '/admin/blog/new', icon: 'blog' as IconName },
  { label: 'Review messages', href: '/admin/messages', icon: 'messages' as IconName },
  { label: 'Update company profile', href: '/admin/settings/company', icon: 'settings' as IconName },
];
