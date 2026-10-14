import { mcpCapabilityLabels } from './mcp-capabilities';

// The role permissions the API defines (AccessPermissions.All), grouped for the role editor. The API
// rejects a name it does not know, so a permission missing here is only missing from the editor.
export const permissionGroups: readonly {
  title: string;
  description: string;
  permissions: Readonly<Record<string, string>>;
}[] = [
  {
    title: 'Users and roles',
    description: 'Accounts, their private details, and what each role may do.',
    permissions: {
      'users.read_private': 'See email addresses and account details',
      'users.manage': 'Create, edit and delete users',
      'roles.manage': 'Create roles and change their permissions',
    },
  },
  {
    title: 'Blog',
    description: 'Drafts and published posts.',
    permissions: pick('list_blog_posts', 'get_blog_post', 'create_blog_post', 'update_blog_post', 'delete_blog_post'),
  },
  {
    title: "FAQ's",
    description: 'Drafts and published answers.',
    permissions: pick('list_faq_entries', 'get_faq_entry', 'create_faq_entry', 'update_faq_entry', 'delete_faq_entry'),
  },
  {
    title: 'Email templates',
    description: 'The emails the system sends.',
    permissions: pick('list_email_templates', 'get_email_template', 'update_email_template', 'preview_email_template', 'reset_email_template'),
  },
  {
    title: 'Inbox and conversations',
    description: 'Messages from site visitors and between members.',
    permissions: {
      'inbox.manage': 'Open the inbox and answer visitor messages',
      'conversations.manage': 'Review closed conversations, and close or delete any conversation',
    },
  },
  {
    title: 'Company',
    description: 'What the public site says about the company.',
    permissions: {
      'company.manage': 'Edit the company profile',
      'github.manage': 'Connect the GitHub repository bug reports go to',
    },
  },
  {
    title: 'Moderation, media and appointments',
    description: 'Reports, account flags, uploaded files and appointment diaries.',
    permissions: {
      'moderation.manage': 'Review reports and account flags',
      'sales.manage': 'Manage catalog, promotions and orders',
      'shipping.manage': 'Manage delivery methods, zones and origins',
      'appointments.manage': "See and book in every account's appointment diary",
      'media.manage': 'See and delete any uploaded file',
    },
  },
  {
    title: 'Member basics',
    description: 'What every signed-in member needs. New roles start with these.',
    permissions: pick('search_area', 'get_my_notification_preferences', 'list_products', 'get_product'),
  },
];

function pick(...capabilities: (keyof typeof mcpCapabilityLabels)[]) {
  return Object.fromEntries(capabilities.map((capability) => [capability, mcpCapabilityLabels[capability]]));
}
