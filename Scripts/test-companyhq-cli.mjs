// One isolated Git lifecycle regression: create, publish/fetch updates, preservation and refusal.
import assert from 'node:assert/strict';
import { cp, mkdir, readFile, writeFile, rename, readdir } from 'node:fs/promises';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { randomUUID } from 'node:crypto';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const area = path.resolve(process.argv[2] ?? path.join(root, '.testagent', `cli-${randomUUID()}`));
const upstream = path.join(area, 'publisher');
const remote = path.join(area, 'upstream.git');
const project = path.join(area, 'consumer');
await mkdir(upstream, { recursive: true });
function command(tool, args, cwd = root, expected = 0) {
  const result = spawnSync(tool, args, { cwd, encoding: 'utf8', windowsHide: true });
  if (result.error) throw result.error;
  assert.equal(result.status, expected, `${tool} ${args[0]}: ${result.stdout}\n${result.stderr}`);
  return `${result.stdout}${result.stderr}`.trim();
}
function git(args, cwd = upstream) { return command('git', args, cwd); }
function cli(args, expected = 0) {
  if (args[0] === 'update') {
    const target = args[2];
    return command('powershell.exe', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', path.join(target, 'Scripts/Update.ps1')], target, expected);
  }
  return command(process.execPath, [path.join(root, 'Scripts/companyhq.mjs'), ...args], root, expected);
}
const excluded = new Set(['node_modules', '.next', 'bin', 'obj', '.git', '.local', 'artifacts', '.env', '.env.dev', '.env.local', '.testagent', 'tsconfig.tsbuildinfo']);
for (const item of ['Apps', 'Services', 'Assets', 'Scripts', 'Templates', 'Tests', 'AGENTS.md', '.gitignore', '.dockerignore', 'docker-compose.yml', 'docker-compose.dev.yml']) {
  await cp(path.join(root, item), path.join(upstream, item), { recursive: true, filter: file => !excluded.has(path.basename(file)) });
}
git(['init', '--initial-branch=main']);
git(['config', 'user.name', 'CompanyHQ CLI Test']);
git(['config', 'user.email', 'cli-test@example.test']);
git(['add', '.']);
git(['commit', '-m', 'Initial shared service snapshot']);
git(['init', '--bare', '--initial-branch=main', remote]);
git(['remote', 'add', 'origin', remote]);
git(['push', '-u', 'origin', 'main']);
const first = git(['rev-parse', 'HEAD']);
cli(['create', '--name', 'Northstar Studio', '--path', project, '--source', remote, '--port-offset', '100']);
for (const file of ['StartDev.ps1', 'StartProd.ps1', 'Update.ps1', 'AGENTS.md']) assert.ok((await readFile(path.join(project, 'Scripts', file), 'utf8')).length);
assert.deepEqual((await readdir(path.join(project, 'Scripts'))).sort(), ['AGENTS.md', 'StartDev.ps1', 'StartProd.ps1', 'Update.ps1']);
assert.equal((await readdir(project)).includes('artifacts'), false);
cli(['create', '--name', 'Overwrite', '--path', project, '--source', remote], 1);
const personalFiles = ['Apps/Web/app/page.tsx', 'Scripts/StartDev.ps1', 'Scripts/AGENTS.md', 'project.json'];
await writeFile(path.join(project, 'Scripts/custom.ps1'), '# Company-owned script\n');
const before = new Map(await Promise.all(personalFiles.map(async file => [file, await readFile(path.join(project, file), 'utf8')])));
const sharedFile = 'Services/Geo/Services/GeoService.cs';
const marker = '// Published generic-service update used by the CLI regression.\n';
await writeFile(path.join(upstream, sharedFile), marker + await readFile(path.join(upstream, sharedFile), 'utf8'));
await writeFile(path.join(upstream, 'Apps/Web/app/page.tsx'), '// An upstream website must never replace a consumer website.\n');
git(['add', '.']); git(['commit', '-m', 'Publish a generic service change']); git(['push', 'origin', 'main']);
const second = git(['rev-parse', 'HEAD']);
assert.notEqual(first, second);
assert.match(cli(['update', '--project', project]), /Updated CompanyHQ/);
const lock = JSON.parse(await readFile(path.join(project, '.companyhq/lock.json'), 'utf8'));
assert.equal(lock.revision, second);
assert.ok((await readFile(path.join(project, '.companyhq/source', sharedFile), 'utf8')).replaceAll('\r\n', '\n').startsWith(marker));
for (const [file, content] of before) assert.equal(await readFile(path.join(project, file), 'utf8'), content);
assert.equal(await readFile(path.join(project, 'Scripts/custom.ps1'), 'utf8'), '# Company-owned script\n');
assert.match(cli(['update', '--project', project]), /already current/);
const checkout = path.join(project, '.companyhq/source');
await writeFile(path.join(checkout, 'untracked-local-change.txt'), 'Preserve me');
assert.match(cli(['update', '--project', project], 1), /local edits/);
await rename(path.join(checkout, 'untracked-local-change.txt'), path.join(area, 'preserved-local-change.txt'));
await rename(checkout, path.join(area, 'preserved-checkout'));
assert.match(cli(['update', '--project', project]), /Restored the locked/);
assert.equal(git(['rev-parse', 'HEAD'], checkout), second);
// A force-pushed older branch must not silently downgrade a consumer.
git(['push', '--force', 'origin', `${first}:refs/heads/main`]);
cli(['update', '--project', project], 1);
assert.equal(git(['rev-parse', 'HEAD'], checkout), second);
assert.equal(JSON.parse(await readFile(path.join(project, '.companyhq/lock.json'), 'utf8')).revision, second);
git(['push', '--force', 'origin', 'main']);
// Put the publisher website back for a fresh fixture suitable for Docker integration CI.
git(['checkout', first, '--', 'Apps/Web/app/page.tsx']);
git(['add', '.']); git(['commit', '-m', 'Restore upstream consumer template']); git(['push', 'origin', 'main']);
cli(['update', '--project', project]);
const linkedProject = path.join(area, 'linked-consumer');
const linkedSource = path.join(area, 'linked-source');
git(['clone', remote, linkedSource]);
await mkdir(path.join(linkedProject, '.companyhq'), { recursive: true });
await writeFile(path.join(linkedProject, '.companyhq/lock.json'), JSON.stringify({ version: 1, mode: 'linked', path: '../linked-source', source: remote, branch: 'main', revision: git(['rev-parse', 'HEAD'], linkedSource) }));
await writeFile(path.join(upstream, sharedFile), marker + await readFile(path.join(upstream, sharedFile), 'utf8'));
git(['add', '.']); git(['commit', '-m', 'Publish shared change for linked consumers']); git(['push', 'origin', 'main']);
await mkdir(path.join(linkedProject, 'Scripts'));
await cp(path.join(project, 'Scripts/Update.ps1'), path.join(linkedProject, 'Scripts/Update.ps1'));
assert.match(cli(['update', '--project', linkedProject]), /Updated CompanyHQ/);
assert.equal(git(['rev-parse', 'HEAD'], linkedSource), git(['rev-parse', 'HEAD']));
await writeFile(path.join(linkedSource, 'local-work.txt'), 'Keep local shared work');
assert.match(cli(['update', '--project', linkedProject], 1), /local edits/);
assert.equal(await readFile(path.join(linkedSource, 'local-work.txt'), 'utf8'), 'Keep local shared work');
cli(['update', '--project', project]);
const report = { passed: ['default scripts on creation', 'refuse existing destination', 'published Git update', 'company file preservation', 'no-op when current', 'refuse dirty dependencies', 'restore locked dependency', 'refuse force-pushed downgrade', 'linked shared-checkout update', 'linked local edits preserved'], project, remote, before: first, after: git(['rev-parse', 'HEAD'], checkout) };
await writeFile(path.join(area, 'companyhq-cli.json'), JSON.stringify(report, null, 2));
console.log(JSON.stringify(report, null, 2));
