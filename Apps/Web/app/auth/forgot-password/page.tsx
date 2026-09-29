import Link from 'next/link';
import AccountForm from '../../../Components/account-form';

export default async function Page({ searchParams }: { searchParams: Promise<{ token?: string }> }) {
  const { token } = await searchParams;
  return (
    <section className="auth-page">
      <header className="auth-page-header">
        <h2>Reset your password</h2>
        <p>Enter the email on your account and we will send a reset link.</p>
      </header>
      <AccountForm kind="forgot-password" layout="auth" token={token} />
      <div className="auth-next-page gap-sm">
        <Link className="button button--ghost" href="/auth/login">Sign in</Link>
      </div>
    </section>
  );
}
