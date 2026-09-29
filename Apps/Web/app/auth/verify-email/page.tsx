import Link from 'next/link';
import AccountForm from '../../../Components/account-form';

export default async function Page({ searchParams }: { searchParams: Promise<{ token?: string }> }) {
  const { token } = await searchParams;
  return (
    <section className="auth-page">
      <header className="auth-page-header">
        <h2>Verify your email</h2>
        <p>Confirm your address to finish setting up your account.</p>
      </header>
      <AccountForm kind="verify-email" layout="auth" token={token} />
      <div className="auth-next-page gap-sm">
        <Link className="button button--ghost" href="/auth/login">Sign in</Link>
      </div>
    </section>
  );
}
