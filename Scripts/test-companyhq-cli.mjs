// Creation regression: the starter a company receives, and the refusal to overwrite one.
import assert from 'node:assert/strict';
import { readFile, readdir, rm, mkdir, lstat } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { randomUUID } from 'node:crypto';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const project = path.resolve(process.argv[2] ?? path.join(root, '.testagent', `cli-${randomUUID()}`));
// Reject even empty directories and dangling symlinks before entering the cleanup block.
const occupied = await lstat(project).then(() => true, error => {
  if (error.code === 'ENOENT') return false;
  throw error;
});
if (occupied) throw new Error('CLI regression requires a nonexistent destination; existing paths are never removed.');
await mkdir(path.dirname(project), { recursive: true });
// Reserve this run's directory exclusively. A failed reservation never enters cleanup.
await mkdir(project);
function cli(args, expected = 0) {
  const result = spawnSync(process.execPath, [path.join(root, 'Scripts/companyhq.mjs'), ...args], { cwd: root, encoding: 'utf8', windowsHide: true });
  if (result.error) throw result.error;
  assert.equal(result.status, expected, `${result.stdout}\n${result.stderr}`);
}
try {
  cli(['create', '--name', 'Northstar Studio', '--path', project, '--port-offset', '100']);
  assert.deepEqual((await readdir(path.join(project, 'Scripts'))).sort(), ['AGENTS.md', 'StartDev.ps1', 'StartProd.ps1', 'Update.ps1']);
  // A clean headless API: no HQ host, demo providers, tests, website, assets, database or auth.
  assert.deepEqual((await readdir(path.join(project, 'Apps'))).sort(), ['Api']);
  assert.deepEqual((await readdir(path.join(project, 'Apps/Api'))).sort(), ['Company.Api.csproj', 'Dockerfile', 'Program.cs', 'appsettings.json']);
  for (const absent of ['Tests', 'Services', 'Assets', 'Apps/Web', 'Apps/Api/LocalProviders.cs', 'Apps/Api/Migrations', '.config', '.companyhq', '.local', '.env.example', 'env.dev', 'artifacts', 'node_modules']) {
    assert.equal(existsSync(path.join(project, absent)), false, absent);
  }
  // Configuration is environment-only: development values in .env.dev; .env carries the same keys, all empty.
  const keys = text => text.split(/\r?\n/).filter(line => /^[A-Za-z_]\w*=/.test(line)).map(line => line.split('=')[0]);
  const development = await readFile(path.join(project, '.env.dev'), 'utf8');
  const production = await readFile(path.join(project, '.env'), 'utf8');
  assert.ok(keys(development).includes('COMPANYHQ_PACKAGES_TOKEN'));
  assert.deepEqual(keys(production), keys(development));
  assert.doesNotMatch(production, /^[A-Za-z_]\w*=.+$/m);
  // Generic services arrive as exact-pinned packages referenced directly, never through CompanyHQ.Api.
  const csproj = await readFile(path.join(project, 'Apps/Api/Company.Api.csproj'), 'utf8');
  const references = [...csproj.matchAll(/PackageReference Include="(CompanyHQ[^"]*)" Version="([^"]+)"/g)].map(match => [match[1], match[2]]);
  assert.deepEqual(references, [['CompanyHQ.Services.Rss', '[$(CompanyHQVersion)]'], ['CompanyHQ.Services.Rss.Contracts', '[$(CompanyHQVersion)]']]);
  const program = await readFile(path.join(project, 'Apps/Api/Program.cs'), 'utf8');
  assert.match(program, /AddRss\(builder\.Configuration\)/);
  assert.match(program, /MapHealthChecks\("\/health"\)/);
  assert.doesNotMatch(program, /Api\.|Northstar|LocalProviders|MapRss/);
  for (const file of ['Apps/Api/Dockerfile', 'docker-compose.yml', 'Scripts/StartDev.ps1', 'Scripts/StartProd.ps1']) {
    assert.doesNotMatch(await readFile(path.join(project, file), 'utf8'), /postgres|redis|mailpit|dotnet ef|Update-Migrations/i, file);
  }
  assert.match(await readFile(path.join(project, 'Directory.Build.props'), 'utf8'), /<CompanyHQVersion>\d+\.\d+\.\d+</);
  assert.match(await readFile(path.join(project, '.gitignore'), 'utf8'), /^\.env$/m);
  const settings = JSON.parse(await readFile(path.join(project, 'project.json'), 'utf8'));
  assert.equal(settings.slug, 'northstar-studio');
  assert.deepEqual(settings.ports, { api: 4202 });
  assert.deepEqual(settings.apps, {});
  cli(['create', '--name', 'Overwrite', '--path', project], 1);
  const guard = spawnSync(process.execPath, [fileURLToPath(import.meta.url), project], { encoding: 'utf8', windowsHide: true });
  assert.equal(guard.status, 1);
  assert.match(guard.stderr, /requires a nonexistent destination/);
  assert.equal(JSON.parse(await readFile(path.join(project, 'project.json'), 'utf8')).slug, 'northstar-studio');
  console.log('companyhq create: passed');
} finally {
  await rm(project, { recursive: true, force: true });
}
