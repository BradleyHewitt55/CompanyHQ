'use client';

import { createContext, useContext, type ReactNode } from 'react';
import { createPortal } from 'react-dom';

/* A page owns its primary action and hands it to the shell's top bar, so the action sits beside the
   account menu without the shell knowing which page is showing. Outside the shell there is no bar
   to hand it to, so the slot renders where it was declared. */
export const TopbarSlots = createContext<{ action: HTMLElement | null } | null>(null);

export function TopbarAction({ children }: { children: ReactNode }) {
  const target = useContext(TopbarSlots)?.action;
  return target ? createPortal(children, target) : <>{children}</>;
}
