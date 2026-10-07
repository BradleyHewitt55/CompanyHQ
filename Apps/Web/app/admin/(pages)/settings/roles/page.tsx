import { requestJson } from '../../../../../src/api/client';
import { requirePermission } from '../../../../../src/api/current-user';
import { areaPermissions } from '../../../../../src/api/access';
import type { PagedResultOfAdminAccessRoleDto } from '../../../../../src/api/types';
import { RoleForm } from './role-form';

export const dynamic = 'force-dynamic';

export const metadata = { title: 'Roles' };

export default async function RolesPage() {
  const user = await requirePermission(areaPermissions.roles);
  const { items: roles } = await requestJson<PagedResultOfAdminAccessRoleDto>('/api/access-roles?pageSize=100');

  return (
    <section aria-labelledby="roles-title" className="settings-page">
      <h1 className="visually-hidden" id="roles-title">Roles</h1>
      <div className="role-list">
        <RoleForm grantable={user.permissions} />
        {roles.map((role) => <RoleForm grantable={user.permissions} key={role.id} role={role} />)}
      </div>
    </section>
  );
}
