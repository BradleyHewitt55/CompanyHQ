import { randomBytes, timingSafeEqual } from 'node:crypto';
import { NextRequest, NextResponse } from 'next/server';
import { requirePermission } from '../../../../../src/api/current-user';
import { areaPermissions } from '../../../../../src/api/access';
import { getConnectedRedis } from '../../../../../src/api/refresh';
import { browserOrigin, githubConfig, githubCookieOptions, githubFlowCookie, githubRequest, githubSelectionCookie } from '../../../../../src/api/github-link';

export async function GET(request: NextRequest) {
  const destination = new URL('/admin/settings/company', browserOrigin(request));
  const response = (reason: string) => NextResponse.redirect(new URL(`${destination.pathname}?github=${reason}`, destination));
  const raw = request.cookies.get(githubFlowCookie)?.value;
  const state = request.nextUrl.searchParams.get('state');
  const code = request.nextUrl.searchParams.get('code');
  const config = githubConfig();
  if (!raw || !state || !code || !config) return response('failed');

  let flow: { state: string; verifier: string; userId: string };
  try { flow = JSON.parse(Buffer.from(raw, 'base64url').toString()); }
  catch { return response('failed'); }
  const expected = Buffer.from(flow.state || '');
  const actual = Buffer.from(state);
  if (expected.length !== actual.length || !timingSafeEqual(expected, actual)) return response('failed');

  const user = await requirePermission(areaPermissions.github);
  if (user.id !== flow.userId) return response('failed');
  const pending = await getConnectedRedis();
  const used = await pending.set(`companyhq:web:github:state:${state}`, 'used', { NX: true, EX: 600 });
  if (!used) return response('failed');

  try {
    const exchange = await fetch('https://github.com/login/oauth/access_token', {
      method: 'POST',
      headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
      body: JSON.stringify({ client_id: config.clientId, client_secret: config.clientSecret, code, redirect_uri: config.callbackUrl, code_verifier: flow.verifier }),
      cache: 'no-store',
      signal: AbortSignal.timeout(10000),
    });
    if (!exchange.ok) throw new Error('GitHub authorization failed.');
    const tokenResult = await exchange.json() as { access_token?: string; error?: string; expires_in?: number; refresh_token?: string };
    if (!tokenResult.access_token || tokenResult.error) throw new Error('GitHub authorization failed.');
    if (tokenResult.expires_in || tokenResult.refresh_token) throw new Error('expiring-token');
    const account = await githubRequest<{ login: string }>('/user', tokenResult.access_token);
    const installations = await githubRequest<{ installations: { permissions: { issues?: string } }[] }>('/user/installations?per_page=100', tokenResult.access_token);
    if (!installations.installations.length) throw new Error('app-not-installed');
    if (!installations.installations.some(installation => installation.permissions.issues === 'write')) throw new Error('issues-permission');
    const key = randomBytes(32).toString('hex');
    await pending.set(`companyhq:web:github:${key}`, JSON.stringify({ userId: user.id, token: tokenResult.access_token, login: account.login }), { EX: 600 });
    const success = NextResponse.redirect(new URL('/admin/settings/github', browserOrigin(request)));
    success.cookies.delete(githubFlowCookie);
    success.cookies.set(githubSelectionCookie, key, githubCookieOptions);
    return success;
  } catch (error) {
    const reason = error instanceof Error && error.message === 'expiring-token' ? 'expiring'
      : error instanceof Error && error.message === 'app-not-installed' ? 'install'
      : error instanceof Error && error.message === 'issues-permission' ? 'permission'
      : 'failed';
    const failed = response(reason);
    failed.cookies.delete(githubFlowCookie);
    return failed;
  }
}
