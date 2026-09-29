import type { Metadata } from 'next';

import LoginForm from '../../../Components/login-form';
import { safeReturnTo as destination } from '../../../src/api/navigation';

export const metadata: Metadata = { title: 'Sign in' };

/* Apps/Mobile Auth/Pages/Login/Login.razor. */
export default async function LoginPage({ searchParams }: { searchParams: Promise<{ returnTo?: string }> }) {
  const { returnTo } = await searchParams;

  return (
    <section className="auth-page">
      <header className="auth-page-header">
        <h2>Welcome back</h2>
        <p>Sign in to your account.</p>
      </header>
      <LoginForm returnTo={destination(returnTo)} />
    </section>
  );
}
