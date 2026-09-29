import Link from 'next/link';
import AccountForm from '../../../Components/account-form';

export const dynamic = 'force-dynamic';

function getMarketingUrl() {
  const configuredMarketingUrl = process.env.MARKETING_URL?.trim();
  if (!configuredMarketingUrl) {
    throw new Error('MARKETING_URL is required for CompanyHQ Web. Set it in the active environment.');
  }

  return configuredMarketingUrl.replace(/\/$/, '');
}

/* Apps/Mobile Auth/Pages/Register/Register.razor. */
export default async function Page({ searchParams }: { searchParams: Promise<{ token?: string }> }) {
  const { token } = await searchParams;
  const marketingUrl = getMarketingUrl();
  return (
    <section className="auth-page">
      <header className="auth-page-header">
        <h2>Create your account</h2>
        <p>Create your account.</p>
      </header>
      <AccountForm kind="signup" layout="auth" marketingUrl={marketingUrl} token={token} />
      <div className="auth-next-page gap-sm">
        <span>Already have an account?</span>
        <Link className="button button--ghost" href="/auth/login">Sign in</Link>
      </div>
    </section>
  );
}
