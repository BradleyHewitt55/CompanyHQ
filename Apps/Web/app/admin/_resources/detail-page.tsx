import type { Metadata } from 'next';
import { notFound } from 'next/navigation';

import { ResourceEditor } from '@/app/admin/_resources/editor-view';
import { getResource } from '../../../Components/admin-data';
import { getAdminRecord } from '../../../src/api/admin';
import { ApiError } from '../../../src/api/client';

export async function generateMetadata({ params }: { params: Promise<{ resource: string; id: string }> }): Promise<Metadata> {
  const { resource: resourceKey, id } = await params;
  const resource = getResource(resourceKey);
  if (!resource) {
    return { title: 'Admin' };
  }

  let existingRecord;
  try {
    existingRecord = id === 'new' ? undefined : await getAdminRecord(resource.key, id);
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return { title: 'Details' };
    throw error;
  }
  return { title: id === 'new' ? `New ${resource.singularLabel}` : existingRecord?.title ?? 'Details' };
}

export default async function ResourceDetailsPage({ params }: { params: Promise<{ resource: string; id: string }> }) {
  const { resource: resourceKey, id } = await params;
  const resource = getResource(resourceKey);

  if (!resource) {
    notFound();
  }

  let existingRecord;
  try {
    existingRecord = id === 'new' ? undefined : await getAdminRecord(resource.key, id);
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  }

  if (id !== 'new' && !existingRecord) notFound();

  return <ResourceEditor recordId={id} record={existingRecord} resource={resource} />;
}
