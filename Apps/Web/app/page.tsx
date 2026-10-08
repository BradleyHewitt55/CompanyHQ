import Link from 'next/link';

import './home.css';
import { CompanyLogo } from '../Components/company-logo';
import { ContactWidget } from '../Components/contact-widget';
import { canOpenAdmin } from '../src/api/access';
import { requestJson } from '../src/api/client';
import { getCompanyProfile } from '../src/api/company';
import { readAccessToken } from '../src/api/session';
import type { AuthUserDto, ClientWidgetDto } from '../src/api/types';
import { logoutAction } from './auth/login/actions';

/** The company's default public website: who they are and how to reach them. */
export default async function HomePage() {
  const company = await getCompanyProfile();
  // The form is left out, not broken, when the API has no widget plugged in or cannot be reached.
  const widget = await requestJson<ClientWidgetDto>('/api/widget').catch(() => undefined);
  // A visitor stays a visitor when there is no session or the API cannot confirm it.
  const user = await readAccessToken() ? await requestJson<AuthUserDto>('/api/auth/me').catch(() => undefined) : undefined;
  const name = company?.name || 'Our company';
  const year = new Date().getFullYear();

  return (
    <div className="site">
      <header className="site-header">
        <Link className="site-brand" href="/">
          <CompanyLogo alt="" className="site-logo" src={company?.logoUrl ?? '/logo-frame3.svg'} />
          <span>{name}</span>
        </Link>
        <nav className="site-nav" aria-label="Main">
          <a href="#about">About</a>
          <a href="#contact">Contact</a>
          {user ? (
            <>
              {canOpenAdmin(user.permissions) && <Link href="/admin">Admin</Link>}
              <form action={logoutAction}>
                <button className="site-nav-cta" type="submit">Sign out</button>
              </form>
            </>
          ) : (
            <Link className="site-nav-cta" href="/auth/login">Sign in</Link>
          )}
        </nav>
      </header>

      <main>
        <section className="site-hero">
          <h1>{name}</h1>
          {company?.description && <p className="site-lead">{company.description}</p>}
          <div className="site-actions">
            <a className="site-button site-button--primary" href="#contact">Get in touch</a>
            {company?.website && (
              <a className="site-button" href={company.website} rel="noreferrer">Visit our website</a>
            )}
          </div>
        </section>

        <section className="site-section" id="about">
          <h2>About us</h2>
          <p>
            {company?.description
              || `${name} will share more about who we are and what we do here soon.`}
          </p>
        </section>

        <section className="site-section" id="contact">
          <h2>Contact</h2>
          {company ? (
            <dl className="site-contact">
              {(company.publicEmails ?? [
                { title: 'General enquiries', address: company.infoEmail, description: null },
                { title: 'Support', address: company.supportEmail, description: null },
              ]).map((email, index) => <div key={`${email.address}-${index}`}>
                <dt>{email.title}</dt>
                <dd><a href={`mailto:${email.address}`}>{email.address}</a>{email.description && <p>{email.description}</p>}</dd>
              </div>)}
            </dl>
          ) : (
            <p>Contact details will appear here once they are published.</p>
          )}
          {widget && (widget.messages || widget.issues) && <ContactWidget widget={widget} />}
        </section>
      </main>

      <footer className="site-footer">
        <span>&copy; {year} {name}</span>
      </footer>
    </div>
  );
}
