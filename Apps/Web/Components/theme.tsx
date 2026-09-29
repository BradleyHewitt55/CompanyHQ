'use client';

import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { Label } from '@/Components/ui/label';
import { RadioGroup, RadioGroupItem } from '@/Components/ui/radio-group';

export type ThemePreference = 'system' | 'light' | 'dark';

type ThemeContextValue = {
  preference: ThemePreference;
  setPreference: (value: ThemePreference) => void;
};

const ThemeContext = createContext<ThemeContextValue | null>(null);
const storageKey = 'companyhq-theme';

function isThemePreference(value: string | null): value is ThemePreference {
  return value === 'system' || value === 'light' || value === 'dark';
}

function applyTheme(preference: ThemePreference) {
  const root = document.documentElement;
  const systemDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
  const resolved = preference === 'system' ? (systemDark ? 'dark' : 'light') : preference;
  root.dataset.theme = preference === 'system' ? '' : preference;
  root.dataset.bsTheme = resolved;
  root.style.colorScheme = resolved;
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [preference, setPreferenceState] = useState<ThemePreference>('system');

  useEffect(() => {
    const stored = window.localStorage.getItem(storageKey);
    if (isThemePreference(stored)) setPreferenceState(stored);
  }, []);

  useEffect(() => {
    applyTheme(preference);
    const media = window.matchMedia('(prefers-color-scheme: dark)');
    const update = () => { if (preference === 'system') applyTheme('system'); };
    media.addEventListener('change', update);
    return () => media.removeEventListener('change', update);
  }, [preference]);

  const value = useMemo(() => ({
    preference,
    setPreference: (next: ThemePreference) => {
      window.localStorage.setItem(storageKey, next);
      setPreferenceState(next);
    },
  }), [preference]);

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function ThemeSelect() {
  const context = useContext(ThemeContext);
  if (!context) throw new Error('ThemeSelect must be rendered inside ThemeProvider');

  return <fieldset className="theme-picker">
    <legend>Appearance</legend>
    <p>Choose how this site looks on this device.</p>
    <RadioGroup aria-label="Appearance" className="theme-picker__options" onValueChange={(value) => { if (isThemePreference(value)) context.setPreference(value); }} value={context.preference}>
      {([
        ['system', 'Default', 'Follow your device'],
        ['light', 'Light', 'Always use light mode'],
        ['dark', 'Dark', 'Always use dark mode'],
      ] as const).map(([value, label, description]) => <div className="theme-picker__option" key={value}>
        <RadioGroupItem id={`theme-${value}`} value={value} />
        <Label htmlFor={`theme-${value}`}><span><strong>{label}</strong><small>{description}</small></span></Label>
      </div>)}
    </RadioGroup>
  </fieldset>;
}
