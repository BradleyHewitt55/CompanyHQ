import 'server-only';

import { ApiError, requestJson } from './client';
import type { ClientCompanyProfileDto } from './types';

/** Null until an administrator has saved the profile. */
export async function getCompanyProfile() {
  try {
    return await requestJson<ClientCompanyProfileDto>('/api/company');
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null;
    throw error;
  }
}
