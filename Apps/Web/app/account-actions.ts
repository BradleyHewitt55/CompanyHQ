'use server';
import { redirect } from 'next/navigation';
import { ApiError, getApiUrl, requestJson } from '../src/api/client';
import { saveSession, readRefreshToken, clearSession } from '../src/api/session';
import { currentUser } from '../src/api/current-user';
import type { RegisterRequest, AuthSessionDto, ChangePasswordRequest, ChangeEmailRequest, ForgotPasswordRequest, ResetPasswordRequest, VerifyEmailRequest, ConfirmEmailChangeRequest, RequestAccountDeletionRequest, ConfirmAccountDeletionRequest, ApiProblem } from '../src/api/types';

export type AccountState = { error: string; success: string };
export async function accountAction(kind: string, _state: AccountState, form: FormData): Promise<AccountState> {
  const field = (name: string) => String(form.get(name) ?? '').trim();
  const password = (name: string) => String(form.get(name) ?? '');
  let path: string;
  let body: RegisterRequest | ForgotPasswordRequest | VerifyEmailRequest | ResetPasswordRequest | ChangePasswordRequest | ChangeEmailRequest | ConfirmEmailChangeRequest;
  switch (kind) {
    case 'signup':
      if (!form.has('consent')) return { error: 'Accept the Terms and Privacy Policy to create your account.', success: '' };
      body = { firstName: field('firstName'), lastName: field('lastName'), email: field('email'), password: password('password') } satisfies RegisterRequest;
      path = 'register'; break;
    case 'forgot-password': body = { email: field('email') } satisfies ForgotPasswordRequest; path = kind; break;
    case 'verify-email': body = { token: field('token') } satisfies VerifyEmailRequest; path = kind; break;
    case 'confirm-email-change': body = { token: field('token') } satisfies ConfirmEmailChangeRequest; path = kind; break;
    case 'reset-password': body = { token: field('token'), newPassword: password('newPassword') } satisfies ResetPasswordRequest; path = kind; break;
    case 'change-password':
      await currentUser();
      body = { currentPassword: password('currentPassword'), newPassword: password('newPassword'), refreshToken: await readRefreshToken() ?? '' } satisfies ChangePasswordRequest;
      path = kind; break;
    case 'change-email':
      await currentUser();
      body = { currentPassword: password('currentPassword'), newEmail: field('newEmail') } satisfies ChangeEmailRequest;
      path = kind; break;
    default: return { error: 'Unknown account action.', success: '' };
  }
  try {
    if (kind.startsWith('change-')) {
      const result = await requestJson<AuthSessionDto>('/api/auth/' + path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
      if (kind === 'change-password' && result?.accessToken) await saveSession(result);
    } else {
      const response = await fetch(getApiUrl() + '/api/auth/' + path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body), cache: 'no-store', signal: AbortSignal.timeout(20000) });
      if (!response.ok) {
        const problem: ApiProblem | null = await response.json().catch(() => null);
        return { error: problem?.detail || problem?.title || 'The request could not be completed. Check your details and try again.', success: '' };
      }
      if (kind === 'signup') await saveSession(await response.json() as AuthSessionDto);
    }
  } catch { return { error: 'We could not complete this request. Check your details and try again.', success: '' }; }
  if (kind === 'signup') redirect('/');
  if (kind === 'verify-email') redirect('/');
  return { error: '', success: kind === 'forgot-password' ? 'If an account exists for this email, a password reset link is on its way.' : kind === 'change-email' ? 'Check your new email address to confirm the change.' : 'Your account has been updated.' };
}
/** First half of deleting the signed-in account: the typed name is checked and a confirmation link is mailed. */
export async function requestAccountDeletion(_state: AccountState, form: FormData): Promise<AccountState> {
  await currentUser();
  const body: RequestAccountDeletionRequest = { fullName: String(form.get('fullName') ?? '').trim() };
  try {
    await requestJson('/api/auth/delete-account', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
  } catch (error) {
    return { error: error instanceof ApiError && error.status === 400 ? 'That is not the name on this account.' : 'The request could not be sent. Try again.', success: '' };
  }
  return { error: '', success: 'Check your email and open the link we sent to finish deleting your account.' };
}

/** Second half, run when the mailed link is opened: redeems the token, which deletes the account. */
export async function confirmAccountDeletion(token: string): Promise<AccountState> {
  const body: ConfirmAccountDeletionRequest = { token };
  try {
    const response = await fetch(getApiUrl() + '/api/auth/confirm-account-deletion', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body), cache: 'no-store', signal: AbortSignal.timeout(20000) });
    if (!response.ok) {
      const problem: ApiProblem | null = await response.json().catch(() => null);
      return { error: problem?.detail || problem?.title || 'This link could not be used.', success: '' };
    }
  } catch { return { error: 'We could not reach the server. Open the link again in a moment.', success: '' }; }
  await clearSession();
  return { error: '', success: 'Your account has been deleted.' };
}
