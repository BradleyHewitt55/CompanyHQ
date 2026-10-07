import { NextRequest, NextResponse } from 'next/server';

import { coordinatedRefresh } from '../../../src/api/refresh';
import { getApiUrl } from '../../../src/api/client';
import { readAccessToken, readRefreshToken, saveSession } from '../../../src/api/session';
import type { AuthSessionDto } from '../../../src/api/types';

export const runtime = 'nodejs';

const oauthFields = ['response_type', 'client_id', 'redirect_uri', 'code_challenge', 'code_challenge_method', 'resource', 'scope', 'state'] as const;

export async function GET(request: NextRequest) {
  const token = await sessionAccessToken();
  if (!token) return loginRedirect(request);

  const params = new URLSearchParams(request.nextUrl.search);
  return new NextResponse(consentPage(params), {
    headers: { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store' },
  });
}

export async function POST(request: NextRequest) {
  const token = await sessionAccessToken();
  if (!token) return loginRedirect(request);

  const form = await request.formData();
  const params = new URLSearchParams();
  for (const field of oauthFields) {
    const value = form.get(field);
    if (typeof value === 'string' && value.length > 0) params.set(field, value);
  }

  const response = await fetch(`${getApiUrl()}/oauth/authorize?${params}`, {
    headers: { Authorization: `Bearer ${token}` },
    cache: 'no-store',
    redirect: 'manual',
  });
  const location = response.headers.get('location');
  if (location && response.status >= 300 && response.status < 400) {
    // Consent is submitted with POST, but the OAuth callback must receive the
    // authorization response as a GET. A 307 would preserve POST and makes
    // the agent callback fail before it can redeem the code.
    return NextResponse.redirect(location, 303);
  }

  if (response.status === 401) return loginRedirect(request);
  return new NextResponse('This OAuth request could not be approved.', {
    status: 400,
    headers: { 'Content-Type': 'text/plain; charset=utf-8', 'Cache-Control': 'no-store' },
  });
}

async function sessionAccessToken() {
  const accessToken = await readAccessToken();
  if (accessToken) return accessToken;

  const refreshToken = await readRefreshToken();
  if (!refreshToken) return undefined;

  let refreshed: AuthSessionDto | null = null;
  try {
    refreshed = await coordinatedRefresh(getApiUrl(), refreshToken);
  } catch {
    return undefined;
  }
  if (!refreshed) return undefined;
  await saveSession(refreshed);
  return refreshed.accessToken;
}

function loginRedirect(request: NextRequest) {
  const login = new URL('/auth/login', request.url);
  login.searchParams.set('returnTo', request.nextUrl.pathname + request.nextUrl.search);
  return NextResponse.redirect(login);
}

function consentPage(params: URLSearchParams) {
  const hidden = oauthFields
    .map((field) => {
      const value = params.get(field);
      return value === null ? '' : `<input type="hidden" name="${field}" value="${escapeHtml(value)}">`;
    })
    .join('');

  return `<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <meta name="theme-color" content="#0d1715">
    <title>Connect an agent</title>
    <style>
      :root { color-scheme: dark; font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; background: #0d1715; color: #effaf6; }
      * { box-sizing: border-box; }
      body { min-height: 100vh; margin: 0; background: radial-gradient(circle at 15% 0%, #173b36 0, #0d1715 42rem), #0d1715; }
      main { display: grid; min-height: 100vh; place-items: center; padding: 2rem 1rem; }
      .consent-card { width: min(100%, 30rem); max-width: 30rem; overflow: hidden; border: 1px solid #2c403b; border-radius: 1.5rem; background: #162420; box-shadow: 0 1.5rem 4rem rgb(0 0 0 / 0.28); }
      .consent-header { padding: 2rem 2rem 1.5rem; border-bottom: 1px solid #2c403b; background: linear-gradient(135deg, rgb(46 119 107 / 0.28), transparent 65%); }
      .brand { display: flex; align-items: center; gap: 0.75rem; margin-bottom: 2.25rem; color: #8ce0cf; font-size: 0.95rem; font-weight: 800; letter-spacing: 0.02em; }
      .brand-mark { width: 2rem; height: 2.35rem; background: currentColor; -webkit-mask: url('/logo-frame3.svg') center / contain no-repeat; mask: url('/logo-frame3.svg') center / contain no-repeat; }
      .eyebrow { margin: 0 0 0.65rem; color: #8ce0cf; font-size: 0.72rem; font-weight: 800; letter-spacing: 0.13em; text-transform: uppercase; }
      h1 { margin: 0; color: #effaf6; font-size: clamp(2rem, 7vw, 2.8rem); letter-spacing: -0.05em; line-height: 1; }
      .lead { max-width: 27rem; margin: 1rem 0 0; color: #cbd8d4; font-size: 1rem; line-height: 1.55; }
      .consent-body { padding: 1.5rem 2rem 2rem; }
      .access-box { display: grid; gap: 0.85rem; margin-bottom: 1.5rem; padding: 1rem; border: 1px solid #49615a; border-radius: 1rem; background: rgb(13 23 21 / 0.62); }
      .access-row { display: flex; gap: 0.75rem; align-items: flex-start; color: #d1e5df; font-size: 0.9rem; line-height: 1.4; }
      .access-icon { flex: 0 0 auto; width: 1.25rem; height: 1.25rem; border-radius: 50%; background: #2e776b; color: #effffb; text-align: center; font-size: 0.78rem; font-weight: 900; line-height: 1.25rem; }
      .note { margin: 0 0 1.5rem; color: #a9bbb6; font-size: 0.82rem; line-height: 1.5; }
      button { width: 100%; min-height: 3rem; border: 0; border-radius: 999px; background: #8ce0cf; color: #08211d; cursor: pointer; font: inherit; font-weight: 800; transition: transform 160ms ease, background 160ms ease; }
      button:hover { background: #a6ecdd; transform: translateY(-1px); }
      button:focus-visible, a:focus-visible { outline: 3px solid #effaf6; outline-offset: 3px; }
      @media (max-width: 30rem) { .consent-header, .consent-body { padding-right: 1.25rem; padding-left: 1.25rem; } .consent-card { border-radius: 1.1rem; } }
      @media (prefers-reduced-motion: reduce) { button { transition: none; } button:hover { transform: none; } }
    </style>
  </head>
  <body>
    <main>
      <section class="consent-card" aria-labelledby="consent-title">
        <header class="consent-header">
          <div class="brand"><span class="brand-mark" aria-hidden="true"></span><span>Agent access</span></div>
          <p class="eyebrow">Agent connection</p>
          <h1 id="consent-title">Connect your account</h1>
          <p class="lead">A trusted agent is asking to connect to your account's tools. Review the access, then approve the connection.</p>
        </header>
        <div class="consent-body">
          <div class="access-box" aria-label="Connection details">
            <div class="access-row"><span class="access-icon" aria-hidden="true">✓</span><span>Use the tools available to your account</span></div>
            <div class="access-row"><span class="access-icon" aria-hidden="true">✓</span><span>Follow the permissions and ownership rules on your account</span></div>
            <div class="access-row"><span class="access-icon" aria-hidden="true">✓</span><span>Be disconnected at any time by revoking its key</span></div>
          </div>
          <p class="note">Only approve this connection if you started it from an agent you trust. Your account permissions still control which tools are available.</p>
          <form method="post">${hidden}<button type="submit">Allow connection</button></form>
        </div>
      </section>
    </main>
  </body>
</html>`;
}

function escapeHtml(value: string) {
  return value.replace(/[&<>'"]/g, (character) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[character] || character));
}
