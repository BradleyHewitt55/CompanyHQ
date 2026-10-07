'use server';

import { revalidatePath } from 'next/cache';

import { requirePermission } from '../../../../src/api/current-user';
import { ApiError, requestJson } from '../../../../src/api/client';
import type {
  AdminEmailTemplateDto,
  AdminEmailTemplatePreviewDto,
  ApiProblem,
  PreviewEmailTemplateRequest,
  UpdateEmailTemplateRequest,
} from '../../../../src/api/types';

export type EmailTemplateActionState = {
  error: string;
  success: string;
};

export type EmailTemplatePreviewState = {
  error: string;
  preview?: AdminEmailTemplatePreviewDto;
};

export async function saveEmailTemplateAction(
  _previousState: EmailTemplateActionState,
  formData: FormData,
): Promise<EmailTemplateActionState> {
  await requirePermission('update_email_template');
  const key = String(formData.get('key') || '').trim();
  const request: UpdateEmailTemplateRequest = { html: String(formData.get('html') || '') };

  try {
    await requestJson<AdminEmailTemplateDto>(`/api/email-templates/${encodeURIComponent(key)}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    });
    revalidatePath('/admin/email-templates');
    return { error: '', success: 'Template saved to the API.' };
  } catch (error) {
    return { error: apiErrorMessage(error), success: '' };
  }
}

export async function resetEmailTemplateAction(
  _previousState: EmailTemplateActionState,
  formData: FormData,
): Promise<EmailTemplateActionState> {
  await requirePermission('reset_email_template');
  const key = String(formData.get('key') || '').trim();

  try {
    await requestJson<AdminEmailTemplateDto>(`/api/email-templates/${encodeURIComponent(key)}/reset`, {
      method: 'POST',
    });
    revalidatePath('/admin/email-templates');
    return { error: '', success: 'Template reset to the packaged default.' };
  } catch (error) {
    return { error: apiErrorMessage(error), success: '' };
  }
}

export async function previewEmailTemplateAction(
  formData: FormData,
): Promise<EmailTemplatePreviewState> {
  await requirePermission('preview_email_template');
  const key = String(formData.get('key') || '').trim();

  try {
    const request: PreviewEmailTemplateRequest = {
      html: String(formData.get('html') || ''),
      values: JSON.parse(String(formData.get('values') || '{}')) as Record<string, string>,
    };
    const preview = await requestJson<AdminEmailTemplatePreviewDto>(
      `/api/email-templates/${encodeURIComponent(key)}/preview`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request),
      },
    );
    return { error: '', preview };
  } catch (error) {
    return { error: apiErrorMessage(error) };
  }
}

function apiErrorMessage(error: unknown) {
  if (error instanceof ApiError) {
    const problem = error.problem as ApiProblem | null;
    return problem?.detail || problem?.title || error.message;
  }
  return error instanceof Error ? error.message : 'The template request failed. Try again.';
}
