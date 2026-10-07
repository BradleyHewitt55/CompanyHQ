import { NextRequest, NextResponse } from 'next/server';
import { ACCESS_TOKEN_COOKIE, REFRESH_TOKEN_COOKIE } from './src/api/cookie-names';
import type { AuthSessionDto, AuthUserDto } from './src/api/types';
import { coordinatedRefresh } from './src/api/refresh';
import { getApiUrl } from './src/api/client';
import { canOpenAdmin } from './src/api/access';

const options = { httpOnly: true, sameSite: 'lax' as const, secure: process.env.NODE_ENV === 'production', path: '/' };

export async function middleware(request: NextRequest) {
  const pathname = request.nextUrl.pathname;
  const publicPage = /^\/auth\/(login|signup|forgot-password|reset-password|verify-email|confirm-email-change|confirm-account-deletion)$/.test(pathname);
  let token = request.cookies.get(ACCESS_TOKEN_COOKIE)?.value;
  const refreshToken = request.cookies.get(REFRESH_TOKEN_COOKIE)?.value;
  let session: AuthSessionDto | undefined;
  let user: AuthUserDto | undefined;
  const api = getApiUrl();
  try {
    let response = token ? await fetch(api + '/api/auth/me', { headers: { Authorization: 'Bearer ' + token }, cache: 'no-store', signal: AbortSignal.timeout(10000) }) : undefined;
    if ((!response || response.status === 401) && refreshToken) {
      const refreshed = await coordinatedRefresh(api, refreshToken);
      if (refreshed) {
        session = refreshed;
        token = session.accessToken;
        response = await fetch(api + '/api/auth/me', { headers: { Authorization: 'Bearer ' + token }, cache: 'no-store', signal: AbortSignal.timeout(10000) });
      }
    }
    if (response?.ok) user = await response.json() as AuthUserDto;
    else if (response && response.status >= 500) throw new Error('Unavailable');
  } catch {
    if (!publicPage) return new NextResponse('The service is temporarily unreachable. Reload this page to try again.', { status: 503, headers: { 'Content-Type': 'text/plain; charset=utf-8', 'Cache-Control': 'no-store' } });
  }

  let result: NextResponse;
  if (!user && !publicPage) {
    const login = new URL('/auth/login', request.url);
    login.searchParams.set('returnTo', pathname + request.nextUrl.search);
    result = NextResponse.redirect(login);
    result.cookies.delete(ACCESS_TOKEN_COOKIE);
    result.cookies.delete(REFRESH_TOKEN_COOKIE);
    return result;
  }
  if (user && (pathname === '/auth/login' || pathname === '/auth/signup')) result = NextResponse.redirect(new URL('/', request.url));
  else if (user && pathname.startsWith('/admin') && !canOpenAdmin(user.permissions)) result = NextResponse.redirect(new URL('/', request.url));
  else {
    const headers = new Headers(request.headers);
    if (session) {
      request.cookies.set(ACCESS_TOKEN_COOKIE, session.accessToken);
      request.cookies.set(REFRESH_TOKEN_COOKIE, session.refreshToken);
      headers.set('cookie', request.cookies.toString());
    }
    result = NextResponse.next({ request: { headers } });
  }
  if (session) {
    result.cookies.set(ACCESS_TOKEN_COOKIE, session.accessToken, { ...options, expires: new Date(session.accessTokenExpiresAt) });
    result.cookies.set(REFRESH_TOKEN_COOKIE, session.refreshToken, { ...options, expires: new Date(session.refreshTokenExpiresAt) });
  }
  result.headers.set('Cache-Control', 'no-store');
  return result;
}
// The public home page is not matched: it needs no session. Only the admin console and the auth
// pages (which redirect a signed-in user away) go through the session check.
export const config = { runtime: 'nodejs', matcher: ['/admin/:path*', '/auth/:path*'] };
