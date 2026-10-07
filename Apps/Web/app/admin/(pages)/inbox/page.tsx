import ResourceListPage from '@/app/admin/_resources/list-page';

export const metadata = { title: 'Inbox' };

export default function Page({ searchParams }: { searchParams: Promise<{ page?: string | string[] }> }) {
  return <ResourceListPage params={Promise.resolve({ resource: 'inbox' })} searchParams={searchParams} />;
}
