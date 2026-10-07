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
import { DeleteResourceButton } from '@/app/admin/_resources/delete-button';
import { TopbarAction } from '@/app/admin/topbar-slots';
const initialActionState: AdminActionState = { error: '', success: '' };

export function ResourceEditor({
  resource,
  record,
  recordId,
  aside,
}: {
  resource: ResourceConfig;
  record?: AdminRecord;
  recordId: string;
  aside?: ReactNode;
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
  const deleteButton = !isNew && record && resource.canDelete && !isEditing ? (
    <DeleteResourceButton label={record.title} onDeleted={() => router.replace(`/admin/${resource.key}`)} recordId={recordId} resource={resource} />
  ) : null;

  if (!canSave) {
    return (
      <section aria-labelledby="editor-title" className="editor-page" data-resource={resource.key}>
        <h1 className="visually-hidden" id="editor-title">{resource.label} details</h1>
        <div className="detail-grid">
          {record ? <ReadOnlyFields record={record} /> : null}
          <aside className="editor-aside">
            {deleteButton ? <div className="editor-aside__actions">{deleteButton}</div> : null}
            {aside}
          </aside>
        </div>
      </section>
    );
  }

  return (
    <section aria-labelledby="editor-title" className="editor-page" data-resource={resource.key}>
      <h1 className="visually-hidden" id="editor-title">{isNew ? `New ${resource.singularLabel}` : `${resource.label} details`}</h1>
      {isEditing && <TopbarAction>
        {isNew ? <Link className="button button-secondary" href={`/admin/${resource.key}`}>Cancel</Link>
          : <button className="button button-secondary" onClick={cancelEditing} type="button">Cancel</button>}
      </TopbarAction>}

      <div className="detail-grid">
        <form action={formAction} aria-busy={pending} className={`form-card${isEditing ? ' is-editing' : ''}`} id="resource-editor-form">
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
          </div>
        </form>

        <aside className="editor-aside">
          <div className="editor-aside__actions">
            {!isNew && resource.canEdit && !isEditing ? (
              <button className="edit-button" onClick={() => setIsEditing(true)} type="button">
                <Icon name="edit" size={16} />
                Edit
              </button>
            ) : null}
            {isEditing ? (
              <>
                <button className="button button-primary" disabled={pending} form="resource-editor-form" type="submit">
                  <Icon name="check" size={17} />
                  {pending ? 'Saving...' : isNew ? `Create ${resource.singularLabel}` : 'Save changes'}
                </button>
              </>
            ) : null}
            {deleteButton}
          </div>
          {aside}
        </aside>
      </div>
    </section>
  );
}

function ReadOnlyFields({ record }: { record: AdminRecord }) {
  return (
    <dl className="detail-list readonly-fields">
      {record.fields.map((field) => (
        // Long values like descriptions and message bodies read better across the full width.
        <div className={field.value.length > 80 ? 'readonly-fields__wide' : undefined} key={`${field.label}-${field.value}`}>
          <dt>{field.label}</dt>
          <dd>{field.href ? <Link href={field.href}>{field.value}</Link> : field.value}</dd>
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
