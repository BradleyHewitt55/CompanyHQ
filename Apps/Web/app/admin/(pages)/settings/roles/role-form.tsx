'use client';

import { useActionState, useState } from 'react';
import { deleteRoleAction, saveRoleAction, type AdminActionState } from '@/app/admin/actions';
import { Icon } from '@/Components/icons';
import { Checkbox } from '@/Components/ui/checkbox';
import { Field, FieldContent, FieldLabel } from '@/Components/ui/field';
import { Input } from '@/Components/ui/input';
import { permissionGroups } from '@/src/api/permissions';
import type { AdminAccessRoleDto } from '@/src/api/types';

const initialActionState: AdminActionState = { error: '', success: '' };

// The API refuses edits to these two; they always match the permission lists in code.
const builtInRoles = ['Admin', 'Client'];

/** One role's name and permissions, or the blank form that creates a role when none is given. */
export function RoleForm({ role, grantable }: { role?: AdminAccessRoleDto; grantable: readonly string[] }) {
  const [state, formAction, pending] = useActionState(saveRoleAction, initialActionState);
  const [deleteError, setDeleteError] = useState('');
  const [deleting, setDeleting] = useState(false);
  const builtIn = role !== undefined && builtInRoles.includes(role.name);
  const id = role?.id ?? 'new';

  async function remove() {
    if (!role || !window.confirm(`Delete the ${role.name} role? This cannot be undone.`)) return;
    setDeleting(true);
    setDeleteError((await deleteRoleAction(role.id)).error);
    setDeleting(false);
  }

  return (
    <form action={formAction} aria-busy={pending} className="form-card settings-form-card">
      <input name="roleId" type="hidden" value={role?.id ?? ''} />
      <div className="card-header">
        <h2 className="card-title">{role ? role.name : 'New role'}</h2>
        {builtIn ? <span className="form-required">Built in, cannot be changed</span> : null}
      </div>
      <div className="card-body form-body">
        {state.error || deleteError ? <div className="form-error" role="alert">{state.error || deleteError}</div> : null}
        {state.success ? <div className="save-notice" role="status"><Icon name="check" size={16} /> {state.success}</div> : null}
        <fieldset className="editor-fields" disabled={builtIn}>
          <div className="field-grid">
            <label className="form-field" htmlFor={`role-name-${id}`}>
              <span>Role name *</span>
              <Input defaultValue={role?.name ?? ''} id={`role-name-${id}`} maxLength={80} name="name" required />
            </label>
            <div className="form-field form-field-wide mcp-access">
              <div className="mcp-capability-groups">
                {permissionGroups.map((group) => (
                  <details className="mcp-capability-group" key={group.title}>
                    <summary className="mcp-capability-group__heading">
                      <h3>{group.title}</h3>
                      <p>{group.description}</p>
                    </summary>
                    <div className="mcp-capability-list">
                      {Object.entries(group.permissions).map(([permission, label]) => {
                        const fieldId = `role-${id}-${permission}`;
                        // A role editor may only hand out what their own role holds.
                        return (
                          <Field key={permission} orientation="horizontal">
                            <Checkbox
                              defaultChecked={role?.permissions.includes(permission) ?? false}
                              disabled={!grantable.includes(permission)}
                              id={fieldId}
                              name="permission"
                              value={permission}
                            />
                            <FieldContent>
                              <FieldLabel htmlFor={fieldId}>{label}</FieldLabel>
                            </FieldContent>
                          </Field>
                        );
                      })}
                    </div>
                  </details>
                ))}
              </div>
            </div>
          </div>
        </fieldset>
        {builtIn ? null : (
          <div>
            <button className="button button-primary" disabled={pending} type="submit">
              <Icon name="check" size={17} />
              {pending ? 'Saving...' : role ? 'Save role' : 'Create role'}
            </button>
            {role ? <button className="button button--danger" disabled={deleting} onClick={() => void remove()} type="button">Delete role</button> : null}
          </div>
        )}
      </div>
    </form>
  );
}
