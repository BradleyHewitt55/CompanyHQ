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

/** The signed-in account, or a 403 when its role holds none of the given permissions. */
export async function requirePermission(...anyOf: string[]) {
  const user = await currentUser();
  if (!anyOf.some((permission) => user.permissions.includes(permission))) {
    throw new ApiError(403, { detail: 'Your role does not allow this.' });
  }
  return user;
}
