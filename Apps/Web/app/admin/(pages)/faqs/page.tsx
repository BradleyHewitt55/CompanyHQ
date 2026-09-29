import ResourceListPage from '@/app/admin/_resources/list-page';

export const metadata = { title: 'Faqs' };

export default function Page({ searchParams }: { searchParams: Promise<{ page?: string | string[] }> }) {
  return <ResourceListPage params={Promise.resolve({ resource: 'faqs' })} searchParams={searchParams} />;
}
