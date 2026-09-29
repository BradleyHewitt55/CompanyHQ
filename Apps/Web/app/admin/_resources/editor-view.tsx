'use client';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useActionState, useEffect, useState, type ReactNode } from 'react';
import { saveResourceAction, type AdminActionState } from '@/app/admin/actions';
import type { AdminRecord, EditorValue, ResourceConfig, ResourceKey } from '@/Components/admin-data';
import { Icon } from '@/Components/icons';
import { useAdminBreadcrumbDetail } from '@/app/admin/breadcrumb';
import { AccountFields } from '@/Components/Admin/account-fields';
import { BlogFields } from '@/app/admin/(pages)/blog/fields';
import { FaqsFields } from '@/app/admin/(pages)/faqs/fields';
const initialActionState: AdminActionState = { error: '', success: '' };

export function ResourceEditor({
  resource,
  record,
  recordId,
}: {
  resource: ResourceConfig;
  record?: AdminRecord;
  recordId: string;
}) {
  const isNew = recordId === 'new';
  useAdminBreadcrumbDetail(isNew ? `New ${resource.singularLabel}` : record?.title ?? 'Details');
  const canSave = isNew ? resource.canCreate : resource.canEdit;
  const router = useRouter();
  const [isEditing, setIsEditing] = useState(isNew);
  const [draftValues, setDraftValues] = useState<Record<string, EditorValue>>();
  const [draftVersion, setDraftVersion] = useState(0);
  const [state, formAction, pending] = useActionState(saveResourceAction, initialActionState);

  useEffect(() => {
    if (state.formValues) {
      setDraftValues(state.formValues);
      setDraftVersion((value) => value + 1);
      return;
    }

    if (state.success) {
      setDraftValues(undefined);
      setIsEditing(false);
      router.refresh();
    }
  }, [router, state.formValues, state.success]);

  const values = isEditing && draftValues ? draftValues : record?.formValues;
  const fieldKey = isEditing ? draftValues ? `editing-error-${draftVersion}` : 'editing' : 'viewing';
  const cancelEditing = () => {
    setDraftValues(undefined);
    setIsEditing(false);
  };

  if (!canSave) {
    return (
      <section aria-labelledby="editor-title" className="editor-page" data-resource={resource.key}>
        <h1 className="visually-hidden" id="editor-title">{resource.label} details</h1>
        <div className="api-empty-card">
          <span className="empty-state-icon"><Icon name={resource.icon} size={22} /></span>
          <h2 className="section-title">Read-only API resource</h2>
          <p>The current API does not expose a write operation for this resource.</p>
          {record ? <ReadOnlyFields record={record} /> : null}
        </div>
      </section>
    );
  }

  return (
    <section aria-labelledby="editor-title" className="editor-page" data-resource={resource.key}>
      <h1 className="visually-hidden" id="editor-title">{isNew ? `New ${resource.singularLabel}` : `${resource.label} details`}</h1>

      <div className="detail-grid">
        <form action={formAction} aria-busy={pending} className={`form-card${isEditing ? ' is-editing' : ''}`}>
          <input name="resource" type="hidden" value={resource.key} />
          <input name="recordId" type="hidden" value={recordId} />
          <div className="card-header">
            <h2 className="card-title">Basic information</h2>
            <span className="form-required">Required fields marked *</span>
          </div>
          <div className="card-body form-body">
            {state.error ? <div className="form-error" role="alert">{state.error}</div> : null}
            {state.success ? <div className="save-notice" role="status"><Icon name="check" size={16} /> {state.success}</div> : null}
            {!isEditing ? <div className="view-mode-note"><Icon name="edit" size={15} /> Select Edit to make changes.</div> : null}
            <fieldset className="editor-fields" disabled={!isEditing} key={fieldKey}>
              <div className="field-grid">
                {renderResourceFields(resource.key, values, isNew)}
              </div>
            </fieldset>
            {isEditing ? (
              <div className="form-actions">
                <button className="button button-primary" disabled={pending} type="submit">
                  <Icon name="check" size={17} />
                  {pending ? 'Saving...' : isNew ? `Create ${resource.singularLabel}` : 'Save changes'}
                </button>
                {isNew ? (
                  <Link className="button button-secondary" href={`/admin/${resource.key}`}>Cancel</Link>
                ) : (
                  <button className="button button-secondary" onClick={cancelEditing} type="button">Cancel</button>
                )}
              </div>
            ) : null}
          </div>
        </form>

        <aside className="editor-aside">
          {!isNew && resource.canEdit ? (
            <div className="editor-aside__actions">
              <button aria-pressed={isEditing} className="edit-button" disabled={isEditing} onClick={() => setIsEditing(true)} type="button">
                <Icon name="edit" size={16} />
                {isEditing ? 'Editing' : 'Edit'}
              </button>
            </div>
          ) : null}
        </aside>
      </div>
    </section>
  );
}

function ReadOnlyFields({ record }: { record: AdminRecord }) {
  return (
    <dl className="detail-list">
      {record.fields.map((field) => (
        <div key={`${field.label}-${field.value}`}>
          <dt>{field.label}</dt>
          <dd>{field.value}</dd>
        </div>
      ))}
    </dl>
  );
}

function renderResourceFields(key: ResourceKey, values: Record<string, EditorValue> | undefined, isNew: boolean): ReactNode {
  switch (key) {
    case 'users': return <AccountFields values={values} isNew={isNew} />;
    case 'blog': return <BlogFields values={values} isNew={isNew} />;
    case 'faqs': return <FaqsFields values={values} isNew={isNew} />;
    default: return null;
  }
}
