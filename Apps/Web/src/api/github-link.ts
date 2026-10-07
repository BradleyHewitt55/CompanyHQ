import 'server-only';

import { cookies } from 'next/headers';
import type { NextRequest } from 'next/server';
import { getConnectedRedis } from './refresh';

export const githubFlowCookie = 'companyhq_github_flow';
export const githubSelectionCookie = 'companyhq_github_selection';
export const githubCookieOptions = {
  httpOnly: true,
  sameSite: 'lax' as const,
  secure: process.env.NODE_ENV === 'production',
  path: '/',
  maxAge: 600,
};

export function browserOrigin(request: NextRequest) {
  const host = request.headers.get('x-forwarded-host') || request.headers.get('host');
  const protocol = request.headers.get('x-forwarded-proto') || request.nextUrl.protocol.replace(':', '');
  if (!host || !['http', 'https'].includes(protocol)) throw new Error('Browser host is unavailable.');
  return `${protocol}://${host}`;
}

export function githubConfig() {
  const clientId = process.env.GITHUB_CLIENT_ID?.trim();
  const clientSecret = process.env.GITHUB_CLIENT_SECRET?.trim();
  const callbackUrl = process.env.GITHUB_CALLBACK_URL?.trim();
  if (!clientId || !clientSecret || !callbackUrl) return null;
  try {
    const callback = new URL(callbackUrl);
    if (callback.protocol !== 'https:' && !(process.env.NODE_ENV !== 'production' && callback.protocol === 'http:')) return null;
  } catch { return null; }
  return { clientId, clientSecret, callbackUrl };
}

export async function githubRequest<T>(path: string, token: string): Promise<T> {
  const response = await fetch(`https://api.github.com${path}`, {
    headers: {
      Accept: 'application/vnd.github+json',
      Authorization: `Bearer ${token}`,
      'X-GitHub-Api-Version': '2022-11-28',
    },
    cache: 'no-store',
    signal: AbortSignal.timeout(10000),
  });
  if (!response.ok) throw new Error(`GitHub returned ${response.status}. Reconnect and try again.`);
  return await response.json() as T;
}

export async function githubSelection(userId: string) {
  const key = (await cookies()).get(githubSelectionCookie)?.value;
  if (!key || !/^[a-f0-9]{64}$/.test(key)) return null;
  const redis = await getConnectedRedis();
  const value = await redis.get(`companyhq:web:github:${key}`);
  if (!value) return null;
  const selection = JSON.parse(value) as { userId: string; token: string; login: string };
  return selection.userId === userId ? { ...selection, key } : null;
}
