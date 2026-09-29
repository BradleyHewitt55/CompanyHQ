'use server';

import { redirect } from 'next/navigation';
import { safeReturnTo } from '../../../src/api/navigation';

import { getApiUrl } from '../../../src/api/client';
import { clearSession, readRefreshToken, saveSession } from '../../../src/api/session';
import type { ApiProblem, AuthSessionDto, LoginRequest, RefreshRequest } from '../../../src/api/types';

export type LoginActionState = {
  error: string;
};

export async function loginAction(
  _previousState: LoginActionState,
  formData: FormData,
): Promise<LoginActionState> {
  const email = String(formData.get('email') || '').trim();
  const password = String(formData.get('password') || '');

  if (!email || !password) {
    return { error: 'Enter your email and password.' };
  }

  const request: LoginRequest = { email, password, deviceLabel: 'Web' };
  let response: Response;

  try {
    response = await fetch(`${getApiUrl()}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
      cache: 'no-store',
    });
  } catch {
    return { error: 'The service is unreachable. Please try again shortly.' };
  }

  const body = await response.json().catch(() => null);
  if (!response.ok) {
    return { error: problemMessage(body as ApiProblem | null) || 'Those credentials were not accepted.' };
  }

  const session = body as AuthSessionDto;
  await saveSession(session);
  redirect(safeReturnTo(String(formData.get('returnTo') || '')));
}

export async function logoutAction(_formData: FormData) {
  const refreshToken = await readRefreshToken();

  if (refreshToken) {
    const request: RefreshRequest = { refreshToken };
    try {
      await fetch(`${getApiUrl()}/api/auth/logout`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request),
        cache: 'no-store',
      });
    } catch {
      // Local logout must still clear the browser session when the API is unavailable.
    }
  }

  await clearSession();
  redirect('/auth/login');
}

function problemMessage(problem: ApiProblem | null) {
  return problem?.detail || problem?.title;
}
