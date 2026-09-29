'use server';

import { revalidatePath } from 'next/cache';
import { redirect } from 'next/navigation';
import { requireAdmin } from '../../src/api/current-user';

import { getResource, type EditorValue } from '../../Components/admin-data';
import { ApiError, requestJson } from '../../src/api/client';
import { removeCompanyLogoFiles, rollbackCompanyLogo, saveCompanyLogo, type SavedCompanyLogo } from '../../lib/company-logo';
import {
  allMcpCapabilities,
  type McpCapability,
} from '../../src/api/mcp-capabilities';
import type {
  AdminUserDto,
  ApiProblem,
  ClientBlogPostDto,
  ClientFaqEntryDto,
  CreateBlogPostRequest,
  CreateFaqEntryRequest,
  CreateUserRequest,
  UpdateBlogPostRequest,
  UpdateFaqEntryRequest,
  UpdateCompanyProfileRequest,
  UpdateProfileRequest,
  UpdateUserRequest,
} from '../../src/api/types';

export type AdminActionState = {
  error: string;
  success: string;
  formValues?: Record<string, EditorValue>;
};

export async function saveResourceAction(
  _previousState: AdminActionState,
  formData: FormData,
): Promise<AdminActionState> {
  await requireAdmin();
  const resource = getResource(String(formData.get('resource') || ''));
  const recordId = String(formData.get('recordId') || 'new');

  if (!resource) return failure('That admin resource does not exist.', formData);
  if (recordId === 'new' && !resource.canCreate) return failure(`The API does not support creating ${resource.label.toLowerCase()}.`, formData);
  if (recordId !== 'new' && !resource.canEdit) return failure(`The API does not support editing ${resource.label.toLowerCase()}.`, formData);

  let routeId: string;
  try {
    routeId = await saveResource(resource.key, recordId, formData);
  } catch (error) {
    return failure(apiErrorMessage(error), formData);
  }

  revalidatePath(`/admin/${resource.key}`);
  revalidatePath('/admin');

  if (recordId === 'new') {
    redirect(`/admin/${resource.key}/${routeId}`);
  }

  return { error: '', success: 'Changes saved to the API.' };
}

export async function updateProfileAction(
  _previousState: AdminActionState,
  formData: FormData,
): Promise<AdminActionState> {
  await requireAdmin();
  const request: UpdateProfileRequest = {
    firstName: value(formData, 'firstName'),
    lastName: value(formData, 'lastName'),
    bio: optionalValue(formData, 'bio'),
    profileImageUrl: optionalValue(formData, 'profileImageUrl'),
  };

  try {
    await requestJson('/api/users/me', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    });
    revalidatePath('/admin/settings/user');
    revalidatePath('/admin');
    return { error: '', success: 'Profile saved to the API.' };
  } catch (error) {
    return failure(apiErrorMessage(error), formData);
  }
}

export async function updateCompanyProfileAction(
  _previousState: AdminActionState,
  formData: FormData,
): Promise<AdminActionState> {
  await requireAdmin();
  const uploadedLogo = formData.get('logo');
  const removeLogo = formData.get('removeLogo') === 'on';
  if (removeLogo && uploadedLogo instanceof File && uploadedLogo.size > 0) {
    return failure('Choose a new logo or remove the current logo, not both.', formData);
  }

  const request: UpdateCompanyProfileRequest = {
    name: value(formData, 'name'),
    website: value(formData, 'website'),
    supportEmail: value(formData, 'supportEmail'),
    infoEmail: value(formData, 'infoEmail'),
    description: value(formData, 'description'),
    logoUrl: removeLogo ? null : optionalValue(formData, 'logoUrl'),
  };

  let savedLogo: SavedCompanyLogo | undefined;
  try {
    if (uploadedLogo instanceof File && uploadedLogo.size > 0) {
      savedLogo = await saveCompanyLogo(uploadedLogo);
      request.logoUrl = savedLogo.url;
    }

    await requestJson('/api/company', jsonPut(request));
    if (savedLogo) {
      await removeCompanyLogoFiles(savedLogo.path);
    } else if (removeLogo) {
      await removeCompanyLogoFiles();
    }
    revalidatePath('/admin/settings/company');
    return { error: '', success: 'Company profile saved to the API.' };
  } catch (error) {
    if (savedLogo) await rollbackCompanyLogo(savedLogo).catch(() => {});
    return failure(apiErrorMessage(error), formData);
  }
}

async function saveResource(
  resource: Exclude<ReturnType<typeof getResource>, undefined>['key'],
  recordId: string,
  formData: FormData,
) {
  const isNew = recordId === 'new';

  switch (resource) {
    case 'users': {
      const roleName = value(formData, 'roleName').trim();
      if (!roleName) throw new Error('Enter a role name.');
      if (isNew) {
        const request: CreateUserRequest = {
          email: value(formData, 'email'),
          password: value(formData, 'password'),
          firstName: optionalValue(formData, 'firstName'),
          lastName: optionalValue(formData, 'lastName'),
          roleName,
          bio: optionalValue(formData, 'bio'),
          mcpDisabledTools: mcpDisabledTools(formData),
        };
        const created = await requestJson<AdminUserDto>('/api/users', jsonPost(request));
        return created.id;
      }

      const request: UpdateUserRequest = {
        email: optionalValue(formData, 'email'),
        firstName: optionalValue(formData, 'firstName'),
        lastName: optionalValue(formData, 'lastName'),
        bio: optionalValue(formData, 'bio'),
        roleName,
        password: optionalValue(formData, 'password'),
        mcpDisabledTools: mcpDisabledTools(formData),
      };
      await requestJson<AdminUserDto>(`/api/users/${encodeURIComponent(recordId)}`, jsonPut(request));
      return recordId;
    }
    case 'blog': {
      if (isNew) {
        const request: CreateBlogPostRequest = {
          slug: value(formData, 'slug'),
          title: value(formData, 'title'),
          description: value(formData, 'description'),
          body: value(formData, 'body'),
          author: value(formData, 'author'),
          category: value(formData, 'category'),
          coverImageUrl: optionalValue(formData, 'coverImageUrl'),
          publishedAt: optionalUtcDateTimeValue(formData, 'publishedAt'),
          sortOrder: integerValue(formData, 'sortOrder'),
          isDraft: booleanValue(formData, 'isDraft'),
        };
        const created = await requestJson<ClientBlogPostDto>('/api/blog', jsonPost(request));
        return created.slug;
      }

      const request: UpdateBlogPostRequest = {
        title: value(formData, 'title'),
        description: value(formData, 'description'),
        body: value(formData, 'body'),
        author: value(formData, 'author'),
        category: value(formData, 'category'),
        coverImageUrl: optionalValue(formData, 'coverImageUrl'),
        publishedAt: optionalUtcDateTimeValue(formData, 'publishedAt'),
        sortOrder: integerValue(formData, 'sortOrder'),
        isDraft: booleanValue(formData, 'isDraft'),
      };
      await requestJson<ClientBlogPostDto>(`/api/blog/${encodeURIComponent(recordId)}`, jsonPut(request));
      return recordId;
    }
    case 'faqs': {
      if (isNew) {
        const request: CreateFaqEntryRequest = {
          key: value(formData, 'key'),
          question: value(formData, 'question'),
          answer: value(formData, 'answer'),
          category: value(formData, 'category'),
          seoDescription: value(formData, 'seoDescription'),
          sortOrder: integerValue(formData, 'sortOrder'),
          publishedAt: optionalUtcDateTimeValue(formData, 'publishedAt'),
          isDraft: booleanValue(formData, 'isDraft'),
        };
        const created = await requestJson<ClientFaqEntryDto>('/api/faqs', jsonPost(request));
        return created.key;
      }

      const request: UpdateFaqEntryRequest = {
        question: value(formData, 'question'),
        answer: value(formData, 'answer'),
        category: value(formData, 'category'),
        seoDescription: value(formData, 'seoDescription'),
        sortOrder: integerValue(formData, 'sortOrder'),
        publishedAt: optionalUtcDateTimeValue(formData, 'publishedAt'),
        isDraft: booleanValue(formData, 'isDraft'),
      };
      await requestJson<ClientFaqEntryDto>(`/api/faqs/${encodeURIComponent(recordId)}`, jsonPut(request));
      return recordId;
    }
    case 'messages':
      throw new Error('This resource is read-only in the current API contract.');
  }
}

function jsonPost(body: unknown): RequestInit {
  return { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) };
}

function jsonPut(body: unknown): RequestInit {
  return { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) };
}

function value(formData: FormData, key: string) {
  return String(formData.get(key) || '').trim();
}

function optionalValue(formData: FormData, key: string) {
  const result = value(formData, key);
  return result || null;
}

function integerValue(formData: FormData, key: string) {
  const parsed = Number.parseInt(value(formData, key) || '0', 10);
  return Number.isFinite(parsed) ? parsed : 0;
}

function optionalUtcDateTimeValue(formData: FormData, key: string) {
  const raw = optionalValue(formData, key);
  if (!raw) return null;

  const date = new Date(`${raw}:00Z`);
  if (Number.isNaN(date.valueOf())) {
    throw new Error('The publication date and time is invalid.');
  }

  return date.toISOString();
}

function booleanValue(formData: FormData, key: string) {
  return value(formData, key) === 'true';
}

function mcpDisabledTools(formData: FormData): McpCapability[] {
  const enabled = new Set(
    formData.getAll('mcpEnabledTool')
      .map(String)
      .filter((capability): capability is McpCapability => allMcpCapabilities.includes(capability as McpCapability)),
  );

  return allMcpCapabilities.filter((capability) => !enabled.has(capability));
}

function failure(error: string, formData?: FormData): AdminActionState {
  return { error, success: '', formValues: formData ? submittedFormValues(formData) : undefined };
}

function submittedFormValues(formData: FormData): Record<string, EditorValue> {
  const values: Record<string, EditorValue> = {};
  for (const [key, raw] of formData.entries()) {
    if (key === 'resource' || key === 'recordId' || key === 'mcpEnabledTool' || typeof raw !== 'string') continue;
    values[key] = raw;
  }

  if (formData.get('resource') === 'users') {
    values.mcpDisabledTools = JSON.stringify(mcpDisabledTools(formData));
  }

  return values;
}

function apiErrorMessage(error: unknown) {
  if (error instanceof ApiError) {
    const problem = error.problem as ApiProblem | null;
    return problem?.detail || problem?.title || error.message;
  }
  return error instanceof Error ? error.message : 'The API request failed. Try again.';
}
