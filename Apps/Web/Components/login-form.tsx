'use client';

import Link from 'next/link';
import { useActionState } from 'react';

import { loginAction, type LoginActionState } from '../app/auth/login/actions';
import { Input } from '@/Components/ui/input';

const initialState: LoginActionState = { error: '' };

/* The field set, copy and shell follow Apps/Mobile Auth/Pages/Login/Login.razor. Mobile opens a
   reset-password modal; on Web the reset link arrives by email, so the same control is a link to
   the /auth/forgot-password route that owns that flow. */
export default function LoginForm({ returnTo }: { returnTo: string }) {
  const [state, formAction, pending] = useActionState(loginAction, initialState);

  return (
    <form action={formAction} className="auth-form gap-md">
      <input name="returnTo" type="hidden" value={returnTo} />

      <div className="auth-inputs gap-md">
        <label className="form-input" htmlFor="login-email">
          <span>Email</span>
          <Input autoCapitalize="none" autoComplete="email" id="login-email" inputMode="email" name="email" placeholder="you@example.com" required type="email" />
        </label>
        <label className="form-input" htmlFor="login-password">
          <span>Password</span>
          <Input autoComplete="current-password" id="login-password" name="password" placeholder="Your password" required type="password" />
        </label>
        <Link className="button button--ghost auth-forgot" href="/auth/forgot-password">Forgot password?</Link>
      </div>

      {state.error ? <p className="auth-error" role="alert">{state.error}</p> : null}

      <div className="auth-actions gap-md">
        <button className="button button--primary auth-submit" disabled={pending} type="submit">Sign in</button>
        <div className="auth-next-page gap-sm">
          <span>New here?</span>
          <Link className="button button--ghost" href="/auth/signup">Create an account</Link>
        </div>
      </div>
    </form>
  );
}
