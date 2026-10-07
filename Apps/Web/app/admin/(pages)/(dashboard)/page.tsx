import { DashboardContent } from '@/app/admin/(pages)/(dashboard)/dashboard-view';
import { getDashboardData } from '../../../../src/api/admin';
import { currentUser } from '../../../../src/api/current-user';

export const metadata = { title: 'Dashboard' };

export default async function AdminDashboardPage() {
  const { permissions } = await currentUser();
  return <DashboardContent data={await getDashboardData(permissions)} permissions={permissions} />;
}
