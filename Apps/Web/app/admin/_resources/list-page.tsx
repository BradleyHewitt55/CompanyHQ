import type { Metadata } from 'next';
import { notFound } from 'next/navigation';

import { ResourceList } from '@/app/admin/_resources/list-view';
import { getAdminResource } from '../../../src/api/admin';

export async function generateMetadata({ params }: { params: Promise<{ resource: string }> }): Promise<Metadata> {
  const { resource: resourceKey } = await params;
  const resource = await getAdminResource(resourceKey);
  return { title: resource?.label ?? 'Admin' };
}

export default async function ResourceListPage({
  params,
  searchParams,
}: {
  params: Promise<{ resource: string }>;
  searchParams: Promise<{ page?: string | string[] }>;
}) {
  const { resource: resourceKey } = await params;
  const { page } = await searchParams;
  const resource = await getAdminResource(resourceKey, parsePage(page));

  if (!resource) {
    notFound();
  }

  return <ResourceList resource={resource} />;
}

function parsePage(value: string | string[] | undefined) {
  const rawValue = Array.isArray(value) ? value[0] : value;
  const page = Number.parseInt(rawValue || '1', 10);
  return Number.isFinite(page) && page > 0 ? page : 1;
}
