import Link from 'next/link';
import AccountForm from '../../../Components/account-form';

export default async function Page({ searchParams }: { searchParams: Promise<{ token?: string }> }) {
  const { token } = await searchParams;
  return (
    <section className="auth-page">
      <header className="auth-page-header">
        <h2>Confirm your email address</h2>
        <p>Confirm the change so your new address is the one we use.</p>
      </header>
      <AccountForm kind="confirm-email-change" layout="auth" token={token} />
      <div className="auth-next-page gap-sm">
        <Link className="button button--ghost" href="/auth/login">Sign in</Link>
      </div>
    </section>
  );
}
