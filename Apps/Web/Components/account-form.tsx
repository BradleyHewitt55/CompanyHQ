'use client';

import { useActionState, useState } from 'react';

import { accountAction } from '../app/account-actions';
import { Checkbox } from '@/Components/ui/checkbox';
import { Input } from '@/Components/ui/input';
import { Label } from '@/Components/ui/label';

/* The field sets and copy follow Apps/Mobile Auth/Pages/Register.razor and App/Pages/User/
   Security.razor. `layout="auth"` renders the AuthLayout shell those pages sit in; Settings uses
   the shared `form` classes instead. */

const submitText: Record<string, string> = {
  signup: 'Create account',
  'forgot-password': 'Send reset link',
  'verify-email': 'Verify email',
  'reset-password': 'Reset password',
  'change-password': 'Change password',
  'change-email': 'Change email',
  'confirm-email-change': 'Confirm email change',
};

function Field({ id, label, name, type = 'text', autoComplete, placeholder, value, onChange }: {
  id: string;
  label: string;
  name: string;
  type?: string;
  autoComplete?: string;
  placeholder?: string;
  value?: string;
  onChange?: (value: string) => void;
}) {
  return <label className="form-input" htmlFor={id}>
    <span>{label}</span>
    <Input
      autoCapitalize={type === 'email' ? 'none' : undefined}
      autoComplete={autoComplete}
      id={id}
      inputMode={type === 'email' ? 'email' : undefined}
      name={name}
      onChange={onChange && ((event) => onChange(event.target.value))}
      placeholder={placeholder}
      required
      type={type}
      value={onChange ? value : undefined}
    />
  </label>;
}

export default function AccountForm({ kind, token = '', marketingUrl = '', layout = 'form', successText }: {
  kind: string;
  token?: string;
  marketingUrl?: string;
  layout?: 'form' | 'auth';
  successText?: string;
}) {
  const [state, action, pending] = useActionState(accountAction.bind(null, kind), { error: '', success: '' });
  const [password, setPassword] = useState('');
  const [confirmation, setConfirmation] = useState('');

  const signup = kind === 'signup';
  const auth = layout === 'auth';
  const newPassword = kind === 'reset-password' || kind === 'change-password';
  const mismatch = newPassword && confirmation.length > 0 && confirmation !== password;

  return <form action={action} className={auth ? 'auth-form gap-md' : 'form'}>
    <div className={auth ? 'auth-inputs gap-md' : 'form__fields'}>
      {signup && <>
        <Field autoComplete="given-name" id="account-first-name" label="First name" name="firstName" placeholder="Jordan" />
        <Field autoComplete="family-name" id="account-last-name" label="Last name" name="lastName" placeholder="Ellis" />
      </>}
      {(signup || kind === 'forgot-password') && <Field autoComplete="email" id="account-email" label="Email" name="email" placeholder="you@example.com" type="email" />}
      {signup && <Field autoComplete="new-password" id="account-password" label="Password" name="password" placeholder="At least 12 characters" type="password" />}
      {kind === 'change-email' && <Field autoComplete="email" id="account-new-email" label="New email" name="newEmail" type="email" />}
      {kind.startsWith('change-') && <Field autoComplete="current-password" id="account-current-password" label="Current password" name="currentPassword" type="password" />}
      {newPassword && <>
        <Field autoComplete="new-password" id="account-new-password" label="New password" name="newPassword" onChange={setPassword} placeholder="At least 12 characters" type="password" value={password} />
        <Field autoComplete="new-password" id="account-confirm-password" label="Confirm new password" name="confirmNewPassword" onChange={setConfirmation} placeholder="Enter it again" type="password" value={confirmation} />
      </>}
      {['verify-email', 'reset-password', 'confirm-email-change'].includes(kind) && (token
        ? <input name="token" type="hidden" value={token} />
        : <Field id="account-token" label="Email confirmation code" name="token" />)}
      {signup && <div className="form__check">
        <Checkbox id="account-consent" name="consent" required />
        <Label htmlFor="account-consent">I agree to the <a href={marketingUrl + '/terms'} rel="noreferrer" target="_blank">Terms</a> and <a href={marketingUrl + '/privacy'} rel="noreferrer" target="_blank">Privacy Policy</a>.</Label>
      </div>}
    </div>

    {mismatch && <p className={auth ? 'auth-error' : 'form__error'} role="alert">New passwords do not match.</p>}
    <div aria-live="polite">
      {state.error && <p className={auth ? 'auth-error' : 'form__error'} role="alert">{state.error}</p>}
      {state.success && <p className={auth ? undefined : 'security-settings__saved'}>{successText ?? state.success}</p>}
    </div>

    <div className={auth ? 'auth-actions gap-md' : 'form__actions'}>
      <button className={'button button--primary' + (auth ? ' auth-submit' : '')} disabled={pending || mismatch} type="submit">
        {pending ? 'Please wait…' : submitText[kind] ?? 'Continue'}
      </button>
    </div>
  </form>;
}
