'use server';

import { revalidatePath } from 'next/cache';
import { redirect } from 'next/navigation';
import { currentUser, requirePermission } from '../../src/api/current-user';
import { areaPermissions } from '../../src/api/access';

import { resourceFor, type EditorValue, type ResourceKey } from '../../Components/admin-data';
import { ApiError, requestJson } from '../../src/api/client';
import { inboxThreadPath } from '../../src/api/inbox';
import { removeCompanyLogoFiles, rollbackCompanyLogo, saveCompanyLogo, type SavedCompanyLogo } from '../../lib/company-logo';
import {
  allMcpCapabilities,
  type McpCapability,
} from '../../src/api/mcp-capabilities';
import type {
  AdminUserDto,
  AdminInboxThreadDto,
  ApiProblem,
  ClientBlogPostDto,
  ClientFaqEntryDto,
  CreateBlogPostRequest,
  CreateMediaUploadRequest,
  MediaUploadTicketDto,
  CreateFaqEntryRequest,
  CreateUserRequest,
  ReplyInboxRequest,
  SaveAccessRoleRequest,
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
  const { permissions } = await currentUser();
  const resource = resourceFor(String(formData.get('resource') || ''), permissions);
  const recordId = String(formData.get('recordId') || 'new');

  if (!resource) return failure('That admin resource does not exist.', formData);
  if (recordId === 'new' && !resource.canCreate) return failure(`Your role cannot create ${resource.label.toLowerCase()}.`, formData);
  if (recordId !== 'new' && !resource.canEdit) return failure(`Your role cannot edit ${resource.label.toLowerCase()}.`, formData);

  let routeId: string;
  let uploadedMediaId: string | undefined;
  let saveAttempted = false;
  const previousCoverUrl = optionalValue(formData, 'coverImageUrl');
  const previousCoverId = optionalValue(formData, 'coverMediaObjectId');
  try {
    if (resource.key === 'blog') {
      optionalUtcDateTimeValue(formData, 'publishedAt');
      const file = formData.get('coverImage');
      if (optionalValue(formData, 'coverImageUrl') !== optionalValue(formData, 'originalCoverImageUrl')) {
        formData.set('coverMediaObjectId', '');
      }
      if (file instanceof File && file.size > 0) {
        if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) throw new Error('Choose a JPEG, PNG, or WebP image.');
        if (file.size > 5 * 1024 * 1024) throw new Error('Choose an image up to 5 MB.');
        const upload: CreateMediaUploadRequest = { scope: 'Content', contentType: file.type, sizeBytes: file.size };
        const ticket = await requestJson<MediaUploadTicketDto>('/api/media', jsonPost(upload));
        uploadedMediaId = ticket.mediaObjectId;
        await requestJson(`/api/media/${uploadedMediaId}/upload`, {
          method: 'PUT',
          headers: { 'Content-Type': file.type, 'Content-Length': String(file.size) },
          body: file,
        });
        await requestJson(`/api/media/${uploadedMediaId}/complete`, { method: 'POST' });
        if (!ticket.url) throw new Error('The image is not ready. Please try again.');
        formData.set('coverImageUrl', ticket.url);
        formData.set('coverMediaObjectId', uploadedMediaId);
      }
    }
    saveAttempted = true;
    routeId = await saveResource(resource.key, recordId, formData);
  } catch (error) {
    if (uploadedMediaId) {
      // A lost response or server error can follow a committed write. Keep the image
      // until a definite rejection proves the blog did not attach it.
      if (!saveAttempted || (error instanceof ApiError && error.status >= 400 && error.status < 500)) {
        await requestJson(`/api/media/${uploadedMediaId}`, { method: 'DELETE' }).catch(() => {});
      }
      formData.set('coverImageUrl', previousCoverUrl || '');
      formData.set('coverMediaObjectId', previousCoverId || '');
    }
    return failure(apiErrorMessage(error), formData);
  }

  revalidatePath(`/admin/${resource.key}`);
  revalidatePath('/admin');

  if (recordId === 'new') {
    redirect(`/admin/${resource.key}/${routeId}`);
  }

  return { error: '', success: 'Changes saved to the API.' };
}

// Route ids are what each API deletes by: blog posts by slug, FAQ entries by key, the rest by id.
const deletePaths: Partial<Record<ResourceKey, string>> = {
  users: '/api/users',
  blog: '/api/blog',
  faqs: '/api/faqs',
};

export async function deleteResourceAction(resourceKey: string, recordId: string): Promise<{ error: string }> {
  const resource = resourceFor(resourceKey, (await currentUser()).permissions);
  const path = resource?.canDelete ? deletePaths[resource.key] : undefined;
  if (!resource || !path) return { error: 'This record cannot be deleted.' };

  try {
    await requestJson(`${path}/${encodeURIComponent(recordId)}`, { method: 'DELETE' });
  } catch (error) {
    return { error: apiErrorMessage(error) };
  }

  revalidatePath(`/admin/${resource.key}`);
  revalidatePath('/admin');
  return { error: '' };
}

/** Re-reads one inbox thread, after replying to it or resolving it when a change is given. */
export async function inboxThreadAction(
  channel: string,
  threadId: string,
  change?: { reply: string } | { resolve: true },
): Promise<{ error: string; thread?: AdminInboxThreadDto }> {
  await requirePermission('inbox.manage');
  const path = inboxThreadPath(channel, threadId);
  try {
    if (change && 'reply' in change) {
      const request: ReplyInboxRequest = { body: change.reply.trim() };
      await requestJson(`${path}/replies`, jsonPost(request));
    } else if (change) {
      await requestJson(`${path}/resolve`, jsonPost({}));
    }
    return { error: '', thread: await requestJson<AdminInboxThreadDto>(path) };
  } catch (error) {
    return { error: apiErrorMessage(error) };
  }
}

/** Permanently removes a thread from a channel that allows it. The API applies the channel's own rule. */
export async function deleteInboxThreadAction(channel: string, threadId: string): Promise<{ error: string }> {
  await requirePermission('inbox.manage');
  try {
    await requestJson(inboxThreadPath(channel, threadId), { method: 'DELETE' });
  } catch (error) {
    return { error: apiErrorMessage(error) };
  }

  revalidatePath('/admin/inbox');
  return { error: '' };
}

export async function updateProfileAction(
  _previousState: AdminActionState,
  formData: FormData,
): Promise<AdminActionState> {
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
  await requirePermission(areaPermissions.company);
  const uploadedLogo = formData.get('logo');
  const removeLogo = formData.get('removeLogo') === 'on';
  if (removeLogo && uploadedLogo instanceof File && uploadedLogo.size > 0) {
    return failure('Choose a new logo or remove the current logo, not both.', formData);
  }

  const request: UpdateCompanyProfileRequest = {
    name: value(formData, 'name'),
    website: value(formData, 'website'),
    supportEmail: value(formData, 'publicEmailAddress_0'),
    infoEmail: value(formData, 'publicEmailAddress_1'),
    publicEmails: Array.from({ length: Math.min(20, Math.max(2, integerValue(formData, 'publicEmailCount'))) }, (_, index) => ({
      title: value(formData, `publicEmailTitle_${index}`),
      address: value(formData, `publicEmailAddress_${index}`),
      description: optionalValue(formData, `publicEmailDescription_${index}`),
    })),
    description: value(formData, 'description'),
    logoUrl: removeLogo ? null : optionalValue(formData, 'logoUrl'),
    facebookAccount: optionalValue(formData, 'facebookAccount'),
    instagramAccount: optionalValue(formData, 'instagramAccount'),
    whatsAppNumber: optionalValue(formData, 'whatsAppNumber'),
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

/** Creates a role, or renames one and replaces its permissions. The API decides what the editor may grant. */
export async function saveRoleAction(
  _previousState: AdminActionState,
  formData: FormData,
): Promise<AdminActionState> {
  await requirePermission(areaPermissions.roles);
  const roleId = value(formData, 'roleId');
  const request: SaveAccessRoleRequest = {
    name: value(formData, 'name'),
    permissions: formData.getAll('permission').map(String),
  };

  try {
    await requestJson(
      roleId ? `/api/access-roles/${encodeURIComponent(roleId)}` : '/api/access-roles',
      roleId ? jsonPut(request) : jsonPost(request),
    );
  } catch (error) {
    return failure(apiErrorMessage(error));
  }

  revalidatePath('/admin/settings/roles');
  return { error: '', success: roleId ? 'Role saved.' : 'Role created.' };
}

/** Removes a role no account or agent connection still uses. */
export async function deleteRoleAction(roleId: string): Promise<{ error: string }> {
  await requirePermission(areaPermissions.roles);
  try {
    await requestJson(`/api/access-roles/${encodeURIComponent(roleId)}`, { method: 'DELETE' });
  } catch (error) {
    return { error: apiErrorMessage(error) };
  }

  revalidatePath('/admin/settings/roles');
  return { error: '' };
}

async function saveResource(
  resource: ResourceKey,
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
          coverMediaObjectId: optionalValue(formData, 'coverMediaObjectId'),
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
        coverMediaObjectId: optionalValue(formData, 'coverMediaObjectId'),
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
    case 'inbox':
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
