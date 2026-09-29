import type { ApiProblem } from './types';

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  let response: Response;
  try {
    response = await fetch('/api/customer' + path, { ...init, cache: 'no-store', headers: { 'Content-Type': 'application/json', ...init.headers } });
  } catch (error) {
    if (error instanceof Error && error.name === 'AbortError') throw error;
    throw new Error('The service is unreachable. Check your connection and try again.');
  }
  if (response.status === 401) {
    window.location.assign('/auth/login?returnTo=' + encodeURIComponent(window.location.pathname + window.location.search));
    throw new Error('Your session has expired. Sign in to continue.');
  }
  if (!response.ok) {
    const problem: (ApiProblem & { error?: string }) | null = await response.json().catch(() => null);
    const code = problem && 'error' in problem && typeof problem.error === 'string' ? problem.error : '';
    const knownMessages: Record<string, string> = {
      sos_location_required: 'Choose an address from the location search before posting the alert.',
    };
    const errors = problem && 'errors' in problem ? Object.values(problem.errors ?? {}).flat().join(' ') : '';
    throw new Error(knownMessages[code] || errors || problem?.detail || (response.status === 403 ? 'This action is not available to your account. If your email is unverified, verify it and try again.' : 'This could not be saved or loaded. Please try again.'));
  }
  if (response.status === 204) return undefined as T;
  return await response.json().catch(() => undefined) as T;
}
export const json = (body: unknown, method = 'POST'): RequestInit => ({ method, body: JSON.stringify(body) });

