import { DashboardContent } from '@/app/admin/(pages)/(dashboard)/dashboard-view';
import { getDashboardData } from '../../../../src/api/admin';

export const metadata = { title: 'Dashboard' };

export default async function AdminDashboardPage() {
  const data = await getDashboardData();
  return <DashboardContent data={data} />;
}
