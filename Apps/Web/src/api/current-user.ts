import 'server-only';
import { cache } from 'react';
import { redirect } from 'next/navigation';
import { ApiError, requestJson } from './client';
import type { AuthUserDto } from './types';

export const currentUser = cache(async () => {
  try { return await requestJson<AuthUserDto>('/api/auth/me'); }
  catch (error) {
    if (error instanceof ApiError && [401, 403].includes(error.status)) redirect('/auth/login');
    throw error;
  }
});
export async function requireAdmin() {
  const user = await currentUser();
  if (user.role !== 'Admin') throw new ApiError(403, { detail: 'Administrator access required.' });
  return user;
}

