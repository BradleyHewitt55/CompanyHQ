import 'server-only';

import { cookies } from 'next/headers';

import { ACCESS_TOKEN_COOKIE, REFRESH_TOKEN_COOKIE } from './cookie-names';
import type { AuthSessionDto } from './types';

export { ACCESS_TOKEN_COOKIE, REFRESH_TOKEN_COOKIE } from './cookie-names';

const cookieOptions = {
  httpOnly: true,
  sameSite: 'lax' as const,
  secure: process.env.NODE_ENV === 'production',
  path: '/',
};

export async function readAccessToken() {
  return (await cookies()).get(ACCESS_TOKEN_COOKIE)?.value;
}

export async function readRefreshToken() {
  return (await cookies()).get(REFRESH_TOKEN_COOKIE)?.value;
}

export async function saveSession(session: AuthSessionDto) {
  const store = await cookies();

  store.set(ACCESS_TOKEN_COOKIE, session.accessToken, {
    ...cookieOptions,
    expires: new Date(session.accessTokenExpiresAt),
  });
  store.set(REFRESH_TOKEN_COOKIE, session.refreshToken, {
    ...cookieOptions,
    expires: new Date(session.refreshTokenExpiresAt),
  });
}

export async function clearSession() {
  const store = await cookies();
  store.delete(ACCESS_TOKEN_COOKIE);
  store.delete(REFRESH_TOKEN_COOKIE);
}
