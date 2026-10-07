'use client';
import { useState, useTransition } from 'react';
import { deleteResourceAction } from '@/app/admin/actions';
import type { ResourceConfig } from '@/Components/admin-data';
import { Icon } from '@/Components/icons';

export function DeleteResourceButton({ resource, recordId, label, onDeleted, compact = false }: {
  resource: ResourceConfig;
  recordId: string;
  label: string;
  onDeleted: () => void;
  compact?: boolean;
}) {
  const [pending, startTransition] = useTransition();
  const [error, setError] = useState('');
  const remove = () => {
    if (!window.confirm(`Delete ${resource.singularLabel.toLowerCase()} "${label}"? This cannot be undone.`)) return;
    setError('');
    startTransition(async () => {
      const result = await deleteResourceAction(resource.key, recordId);
      if (result.error) setError(result.error);
      else onDeleted();
    });
  };

  return <>
    <button
      aria-label={compact ? `Delete ${label}` : undefined}
      className={compact ? 'row-delete-button' : 'button button--danger'}
      disabled={pending}
      onClick={remove}
      title={compact ? 'Delete' : undefined}
      type="button"
    >
      <Icon name="trash3" size={compact ? 16 : 17} />
      {compact ? null : pending ? 'Deleting...' : 'Delete'}
    </button>
    {error ? <p className="form-error delete-error" role="alert">{error}</p> : null}
  </>;
}
