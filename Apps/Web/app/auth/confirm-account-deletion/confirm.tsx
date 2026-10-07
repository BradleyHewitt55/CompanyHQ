'use client';

import { useEffect, useRef, useState } from 'react';
import { confirmAccountDeletion, type AccountState } from '../../account-actions';

/* Opening the mailed link is the confirmation, so the token is redeemed as soon as the page has
   loaded. It runs from the browser rather than during the page request so a mail scanner that only
   fetches the link cannot delete the account. */
export function ConfirmAccountDeletion({ token }: { token: string }) {
  const [state, setState] = useState<AccountState>();
  const started = useRef(false);

  useEffect(() => {
    if (started.current) return;
    started.current = true;
    void confirmAccountDeletion(token).then(setState);
  }, [token]);

  if (!state) return <p aria-live="polite">Removing your account…</p>;
  return state.error
    ? <p className="auth-error" role="alert">{state.error}</p>
    : <p aria-live="polite">{state.success}</p>;
}
