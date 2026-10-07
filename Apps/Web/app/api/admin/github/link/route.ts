import { NextRequest, NextResponse } from 'next/server';
import { requirePermission } from '../../../../../src/api/current-user';
import { areaPermissions } from '../../../../../src/api/access';
import { ApiError, requestJson } from '../../../../../src/api/client';
import { getConnectedRedis } from '../../../../../src/api/refresh';
import { browserOrigin, githubRequest, githubSelection, githubSelectionCookie } from '../../../../../src/api/github-link';

export async function POST(request: NextRequest) {
  const origin = request.headers.get('origin');
  const site = browserOrigin(request);
  if (origin !== site) return new NextResponse('Invalid origin.', { status: 403 });
  const user = await requirePermission(areaPermissions.github);
  const selection = await githubSelection(user.id);
  if (!selection) return NextResponse.redirect(new URL('/admin/settings/company?github=expired', site), 303);
  const data = await request.formData();
  const repository = String(data.get('repository') || '');
  if (!/^[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+$/.test(repository)) return NextResponse.redirect(new URL('/admin/settings/github?error=invalid', site), 303);

  try {
    const verified = await githubRequest<{ full_name: string; private: boolean; has_issues: boolean; permissions?: { pull?: boolean } }>(`/repos/${repository}`, selection.token);
    if (verified.full_name.toLowerCase() !== repository.toLowerCase() || !verified.has_issues || !verified.permissions?.pull) throw new Error('Repository is ineligible.');
    await requestJson('/api/github', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ repository: verified.full_name, accessToken: selection.token }) });
    await getConnectedRedis().then(redis => redis.del(`companyhq:web:github:${selection.key}`)).catch(() => {});
    const response = NextResponse.redirect(new URL('/admin/settings/company?github=linked', site), 303);
    response.cookies.delete(githubSelectionCookie);
    return response;
  } catch (error) {
    const reason = error instanceof ApiError && error.status === 503 ? 'setup' : 'invalid';
    return NextResponse.redirect(new URL(`/admin/settings/github?error=${reason}`, site), 303);
  }
}
