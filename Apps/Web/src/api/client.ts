import 'server-only';

import { readAccessToken } from './session';

export function getApiUrl() {
  const configuredApiUrl = process.env.API_INTERNAL_URL?.trim();
  if (!configuredApiUrl) {
    throw new Error('API_INTERNAL_URL is required for CompanyHQ Web. Set it in the active environment.');
  }

  return configuredApiUrl.replace(/\/$/, '');
}

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly problem: unknown,
  ) {
    super(`API request failed with status ${status}.`);
    this.name = 'ApiError';
  }
}

export async function requestJson<T>(path: string, init: RequestInit = {}) {
  const headers = new Headers(init.headers);
  const accessToken = await readAccessToken();

  if (accessToken) {
    headers.set('Authorization', `Bearer ${accessToken}`);
  }

  const response = await fetch(`${getApiUrl()}${path}`, {
    ...init,
    cache: 'no-store',
    headers,
  });
  const body = await response.json().catch(() => null);

  if (!response.ok) {
    throw new ApiError(response.status, body);
  }

  return body as T;
}
