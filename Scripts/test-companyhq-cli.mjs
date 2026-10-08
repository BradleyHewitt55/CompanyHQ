// Creation regression: the starter a company receives, and the refusal to overwrite one.
import assert from 'node:assert/strict';
import { readFile, readdir, rm } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { randomUUID } from 'node:crypto';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const project = path.resolve(process.argv[2] ?? path.join(root, '.testagent', `cli-${randomUUID()}`));
function cli(args, expected = 0) {
  const result = spawnSync(process.execPath, [path.join(root, 'Scripts/companyhq.mjs'), ...args], { cwd: root, encoding: 'utf8', windowsHide: true });
  if (result.error) throw result.error;
  assert.equal(result.status, expected, `${result.stdout}\n${result.stderr}`);
}
try {
  cli(['create', '--name', 'Northstar Studio', '--path', project, '--port-offset', '100']);
  assert.deepEqual((await readdir(path.join(project, 'Scripts'))).sort(), ['AGENTS.md', 'StartDev.ps1', 'StartProd.ps1', 'Update.ps1']);
  // Both start scripts write the company's migrations themselves; the starter ships none.
  for (const file of ['StartDev.ps1', 'StartProd.ps1']) assert.match(await readFile(path.join(project, 'Scripts', file), 'utf8'), /^Update-Migrations\r?$/m);
  assert.equal(existsSync(path.join(project, 'Apps/Api/Migrations')), false);
  // Generic services arrive as packages: no HQ source, local state or secrets in a new company.
  for (const absent of ['.companyhq', '.local', '.env', 'artifacts']) assert.equal(existsSync(path.join(project, absent)), false, absent);
  assert.match(await readFile(path.join(project, 'Apps/Api/Company.Api.csproj'), 'utf8'), /PackageReference Include="CompanyHQ\.Api"/);
  assert.match(await readFile(path.join(project, 'Directory.Build.props'), 'utf8'), /<CompanyHQVersion>\d+\.\d+\.\d+</);
  assert.match(await readFile(path.join(project, '.gitignore'), 'utf8'), /^\.env$/m);
  const settings = JSON.parse(await readFile(path.join(project, 'project.json'), 'utf8'));
  assert.equal(settings.slug, 'northstar-studio');
  assert.equal(settings.ports.web, 3204);
  const scripts = JSON.parse(await readFile(path.join(project, 'Apps/Web/package.json'), 'utf8')).scripts;
  assert.equal(scripts['api:types'], 'node api-types.mjs');
  assert.ok(existsSync(path.join(project, 'Apps/Web/api-types.mjs')));
  cli(['create', '--name', 'Overwrite', '--path', project], 1);
  console.log('companyhq create: passed');
} finally {
  await rm(project, { recursive: true, force: true });
}
