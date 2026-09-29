import { getEmailTemplates } from '../../../../src/api/admin';
import { EmailTemplatesView } from './email-templates-view';

export const dynamic = 'force-dynamic';

export const metadata = {
  title: 'Email templates',
};

export default async function EmailTemplatesPage() {
  return <EmailTemplatesView templates={await getEmailTemplates()} />;
}
