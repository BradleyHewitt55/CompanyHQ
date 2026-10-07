import { createHash, randomBytes } from 'node:crypto';
import { NextRequest, NextResponse } from 'next/server';
import { requirePermission } from '../../../../../src/api/current-user';
import { areaPermissions } from '../../../../../src/api/access';
import { browserOrigin, githubConfig, githubCookieOptions, githubFlowCookie } from '../../../../../src/api/github-link';

export async function GET(request: NextRequest) {
  const user = await requirePermission(areaPermissions.github);
  const config = githubConfig();
  const origin = browserOrigin(request);
  if (!config) return NextResponse.redirect(new URL('/admin/settings/company?github=unconfigured', origin));
  if (new URL(config.callbackUrl).origin !== origin) {
    return NextResponse.redirect(new URL('/admin/settings/company?github=origin', origin));
  }

  const state = randomBytes(32).toString('hex');
  const verifier = randomBytes(32).toString('base64url');
  const challenge = createHash('sha256').update(verifier).digest('base64url');
  const url = new URL('https://github.com/login/oauth/authorize');
  url.searchParams.set('client_id', config.clientId);
  url.searchParams.set('redirect_uri', config.callbackUrl);
  url.searchParams.set('state', state);
  url.searchParams.set('code_challenge', challenge);
  url.searchParams.set('code_challenge_method', 'S256');
  const response = NextResponse.redirect(url);
  response.cookies.set(githubFlowCookie, Buffer.from(JSON.stringify({ state, verifier, userId: user.id })).toString('base64url'), githubCookieOptions);
  return response;
}
