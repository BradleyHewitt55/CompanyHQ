import { areaPermissions } from '../../../../src/api/access';
import { getEmailTemplates } from '../../../../src/api/admin';
import { requirePermission } from '../../../../src/api/current-user';
import { EmailTemplatesView } from './email-templates-view';

export const dynamic = 'force-dynamic';

export const metadata = {
  title: 'Email templates',
};

export default async function EmailTemplatesPage() {
  await requirePermission(areaPermissions.emailTemplates);
  return <EmailTemplatesView templates={await getEmailTemplates()} />;
}
