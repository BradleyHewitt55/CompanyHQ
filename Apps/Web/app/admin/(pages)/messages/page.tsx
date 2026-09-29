import ResourceListPage from '@/app/admin/_resources/list-page';

export const metadata = { title: 'Messages' };

export default function Page({ searchParams }: { searchParams: Promise<{ page?: string | string[] }> }) {
  return <ResourceListPage params={Promise.resolve({ resource: 'messages' })} searchParams={searchParams} />;
}
