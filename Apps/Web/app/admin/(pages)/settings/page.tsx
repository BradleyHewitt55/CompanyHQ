import { redirect } from 'next/navigation';

import { areaPermissions } from '../../../../src/api/access';
import { currentUser } from '../../../../src/api/current-user';

export default async function SettingsPage() {
  const { permissions } = await currentUser();
  redirect(permissions.includes(areaPermissions.company) ? '/admin/settings/company' : '/admin/settings/user');
}
