import type { Metadata } from 'next';
import { notFound } from 'next/navigation';

import { SettingsPage } from '@/app/admin/(pages)/settings/settings-view';
import { getCompanyProfile } from '../../../../../src/api/company';
import { requestJson } from '../../../../../src/api/client';
import type { AuthUserDto, ClientUserDto } from '../../../../../src/api/types';

const settingSections = ['company', 'user'] as const;

export async function generateMetadata({ params }: { params: Promise<{ section: string }> }): Promise<Metadata> {
  const { section } = await params;
  return { title: section === 'user' ? 'User Profile' : 'Company Profile' };
}

export default async function SettingsSectionPage({ params }: { params: Promise<{ section: string }> }) {
  const { section } = await params;

  if (!settingSections.includes(section as (typeof settingSections)[number])) {
    notFound();
  }

  const currentUser = await requestJson<AuthUserDto>('/api/auth/me');
  const profile = section === 'user'
    ? await requestJson<ClientUserDto>(`/api/users/${encodeURIComponent(currentUser.id)}`)
    : undefined;
  const company = section === 'company' ? await getCompanyProfile() : null;

  return (
    <SettingsPage
      company={company}
      currentUser={currentUser}
      profile={profile}
      section={section as (typeof settingSections)[number]}
    />
  );
}
