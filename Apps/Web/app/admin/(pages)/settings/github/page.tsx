import Link from 'next/link';
import { redirect } from 'next/navigation';
import { requirePermission } from '../../../../../src/api/current-user';
import { areaPermissions } from '../../../../../src/api/access';
import { githubRequest, githubSelection } from '../../../../../src/api/github-link';
import { TopbarAction } from '@/app/admin/topbar-slots';

export default async function GitHubRepositoryPage({ searchParams }: { searchParams: Promise<{ page?: string; error?: string }> }) {
  const user = await requirePermission(areaPermissions.github);
  const selection = await githubSelection(user.id);
  if (!selection) redirect('/admin/settings/company?github=expired');
  const query = await searchParams;
  const page = Math.max(1, Math.min(100, Number.parseInt(query.page || '1', 10) || 1));
  let repositories: { full_name: string; private: boolean; has_issues: boolean; permissions?: { pull?: boolean } }[];
  try {
    repositories = await githubRequest(`/user/repos?visibility=all&affiliation=owner,collaborator,organization_member&sort=full_name&direction=asc&per_page=100&page=${page}`, selection.token);
  } catch {
    redirect('/admin/settings/company?github=failed');
  }
  const choices = repositories.filter(repository => repository.has_issues && repository.permissions?.pull);

  return <section className="settings-page" aria-labelledby="github-repositories-title">
    <TopbarAction><Link className="button button-secondary" href="/admin/settings/company">Cancel</Link></TopbarAction>
    <div className="form-card settings-form-card">
      <div className="card-header"><h1 className="card-title" id="github-repositories-title">Link a GitHub repository</h1></div>
      <div className="card-body form-body">
        <p>Signed in to GitHub as <strong>{selection.login}</strong>. Choose an installed repository with Issues enabled.</p>
        {query.error === 'setup' ? <p className="form-error" role="alert">Set GitHub__TokenEncryptionKey on the API server, then try again.</p> : query.error ? <p className="form-error" role="alert">That repository could not be linked. Try another.</p> : null}
        {choices.length ? <div className="github-repositories">
          {choices.map(repository => <form action="/api/admin/github/link" method="post" key={repository.full_name}>
            <input name="repository" type="hidden" value={repository.full_name} />
            <button className="button button-secondary" type="submit">{repository.full_name}{repository.private ? ' (Private)' : ''}</button>
          </form>)}
        </div> : <p>No eligible repositories on this page.</p>}
        <div className="github-repositories__navigation">
          {page > 1 && <Link href={`/admin/settings/github?page=${page - 1}`}>Previous</Link>}
          {repositories.length === 100 && <Link href={`/admin/settings/github?page=${page + 1}`}>Next</Link>}
        </div>
      </div>
    </div>
  </section>;
}
