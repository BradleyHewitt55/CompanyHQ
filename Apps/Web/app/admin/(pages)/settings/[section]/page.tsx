import type { Metadata } from 'next';
import { notFound } from 'next/navigation';

import { SettingsPage } from '@/app/admin/(pages)/settings/settings-view';
import { getCompanyProfile } from '../../../../../src/api/company';
import { areaPermissions } from '../../../../../src/api/access';
import { requestJson } from '../../../../../src/api/client';
import { currentUser as readCurrentUser } from '../../../../../src/api/current-user';
import type { AdminGitHubConnectionDto, ClientUserDto } from '../../../../../src/api/types';

const settingSections = ['company', 'user'] as const;

export async function generateMetadata({ params }: { params: Promise<{ section: string }> }): Promise<Metadata> {
  const { section } = await params;
  return { title: section === 'user' ? 'User Profile' : 'Company Profile' };
}

export default async function SettingsSectionPage({ params, searchParams }: { params: Promise<{ section: string }>; searchParams: Promise<{ github?: string }> }) {
  const { section } = await params;

  if (!settingSections.includes(section as (typeof settingSections)[number])) {
    notFound();
  }

  const currentUser = await readCurrentUser();
  if (section === 'company' && !currentUser.permissions.includes(areaPermissions.company)) {
    notFound();
  }

  const profile = section === 'user'
    ? await requestJson<ClientUserDto>(`/api/users/${encodeURIComponent(currentUser.id)}`)
    : undefined;
  const company = section === 'company' ? await getCompanyProfile() : null;
  // Only a role that may connect the repository is told which one is connected.
  const github = section === 'company' && currentUser.permissions.includes(areaPermissions.github)
    ? await requestJson<AdminGitHubConnectionDto>('/api/github')
    : undefined;
  const githubStatus = (await searchParams).github;

  return (
    <SettingsPage
      company={company}
      currentUser={currentUser}
      profile={profile}
      section={section as (typeof settingSections)[number]}
      github={github}
      githubStatus={githubStatus}
    />
  );
}
