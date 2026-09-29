import ResourceDetailsPage, { generateMetadata as resourceMetadata } from '@/app/admin/_resources/detail-page';

type Props = { params: Promise<{ id: string }> };
const resourceParams = async (params: Props['params']) => ({ ...await params, resource: 'users' });

export async function generateMetadata({ params }: Props) {
  return resourceMetadata({ params: resourceParams(params) });
}

export default function Page({ params }: Props) {
  return <ResourceDetailsPage params={resourceParams(params)} />;
}
