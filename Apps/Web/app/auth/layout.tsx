import './auth.css';
import { CompanyLogo } from '../../Components/company-logo';
import { getCompanyProfile } from '../../src/api/company';

/* Apps/Mobile Auth/Layout/AuthLayout.razor: one shell for every signed-out screen. */
export default async function AuthLayout({ children }: { children: React.ReactNode }) {
  const company = await getCompanyProfile();
  return (
    <main className="auth-layout">
      <header className="auth-header">
        <CompanyLogo alt={`${company?.name || 'Company'} logo`} className="auth-logo" src={company?.logoUrl ?? '/logo-frame3.svg'} />
        <h1 id="auth-title">{company?.name}</h1>
      </header>
      {children}
    </main>
  );
}
