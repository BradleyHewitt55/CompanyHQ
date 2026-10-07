import Link from 'next/link';
import { ConfirmAccountDeletion } from './confirm';

export const metadata = { title: 'Delete account' };

export default async function Page({ searchParams }: { searchParams: Promise<{ token?: string }> }) {
  const { token } = await searchParams;
  return (
    <section className="auth-page">
      <header className="auth-page-header">
        <h2>Deleting your account</h2>
      </header>
      {token
        ? <ConfirmAccountDeletion token={token} />
        : <p className="auth-error" role="alert">This link is missing its confirmation code. Open the link from the email again.</p>}
      <div className="auth-next-page gap-sm">
        <Link className="button button--ghost" href="/">Back to the site</Link>
      </div>
    </section>
  );
}
