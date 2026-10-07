import { resourceKeys, resources } from '../../Components/admin-data';

/** Console areas outside the resource lists, and the role permission that opens each. */
export const areaPermissions = {
  emailTemplates: 'list_email_templates',
  company: 'company.manage',
  roles: 'roles.manage',
  github: 'github.manage',
} as const;

/**
 * Whether a role holds anything the admin console offers. Only decides what the console shows: the
 * API checks every call against the role itself.
 */
export function canOpenAdmin(permissions: readonly string[]) {
  return [
    ...resourceKeys.flatMap((key) => resources[key].permissions.view),
    ...Object.values(areaPermissions),
  ].some((permission) => permissions.includes(permission));
}
