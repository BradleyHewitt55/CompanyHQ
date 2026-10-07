'use client';

import { useActionState } from 'react';
import { requestAccountDeletion } from '@/app/account-actions';
import { Icon } from '@/Components/icons';
import { Input } from '@/Components/ui/input';

/** Starts deleting the signed-in account: the holder types their name, then confirms from an emailed link. */
export function DeleteAccountCard({ fullName }: { fullName: string }) {
  const [state, action, pending] = useActionState(requestAccountDeletion, { error: '', success: '' });

  return (
    <form action={action} aria-busy={pending} className="form-card settings-form-card">
      <div className="card-header">
        <h2 className="card-title">Delete account</h2>
      </div>
      <div className="card-body form-body">
        {state.error ? <div className="form-error" role="alert">{state.error}</div> : null}
        {state.success ? <div className="save-notice" role="status"><Icon name="check" size={16} /> {state.success}</div> : null}
        <p>Deleting your account removes it and everything stored with it. Type your name to confirm; we then email you a link, and opening it deletes the account.</p>
        <label className="form-field" htmlFor="delete-account-name">
          <span>Type {fullName} to confirm *</span>
          <Input autoComplete="off" id="delete-account-name" maxLength={254} name="fullName" required />
        </label>
        <div>
          <button className="button button--danger" disabled={pending} type="submit">{pending ? 'Sending...' : 'Email me the confirmation link'}</button>
        </div>
      </div>
    </form>
  );
}
