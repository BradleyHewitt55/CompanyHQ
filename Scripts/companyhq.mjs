import { cp, mkdir, readFile, writeFile, readdir, rename, rm, rmdir } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { randomUUID } from 'node:crypto';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const defaultSource = 'https://github.com/BradleyHewitt55/CompanyHQ.git';
function git(args, cwd) {
  const result = spawnSync('git', args, { cwd, encoding: 'utf8', windowsHide: true });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`git ${args[0]} failed: ${result.stderr.trim()}`);
  return result.stdout.trim();
}
async function json(file) { return JSON.parse(await readFile(file, 'utf8')); }
async function save(file, value) {
  await mkdir(path.dirname(file), { recursive: true });
  const temporary = `${file}.${randomUUID()}.tmp`;
  await writeFile(temporary, `${JSON.stringify(value, null, 2)}\n`);
  await rename(temporary, file);
}
function options(args) {
  const result = {};
  while (args.length) {
    const key = args.shift();
    if (!/^--[a-z-]+$/.test(key) || !args.length || args[0].startsWith('--')) throw new Error(`Expected --option value, received ${key}.`);
    if (result[key.slice(2)]) throw new Error(`Duplicate option ${key}.`);
    result[key.slice(2)] = args.shift();
  }
  return result;
}
function validateOptions(opts, allowed) {
  for (const key of Object.keys(opts)) if (!allowed.includes(key)) throw new Error(`Unknown option --${key}.`);
}
function validateLock(lock) {
  if (lock.version !== 1 || typeof lock.source !== 'string' || !lock.source || typeof lock.branch !== 'string' || !/^[a-f0-9]{40,64}$/.test(lock.revision)) throw new Error('Invalid CompanyHQ lock file.');
  git(['check-ref-format', `refs/heads/${lock.branch}`], root);
}
const ignored = new Set(['node_modules', '.next', 'bin', 'obj', '.git', '.local', 'artifacts', '.env', '.env.dev', '.env.local', 'tsconfig.tsbuildinfo']);
async function copy(source, destination) {
  await cp(source, destination, { recursive: true, filter: file => !ignored.has(path.basename(file)) });
}
async function create(opts) {
  validateOptions(opts, ['name', 'path', 'source', 'branch', 'port-offset']);
  if (!opts.name || !opts.path) throw new Error('create requires --name and --path.');
  const destination = path.resolve(opts.path);
  if (existsSync(destination) && (await readdir(destination)).length) throw new Error('Create requires an empty destination; existing projects are never overwritten.');
  const slug = opts.name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
  if (!slug) throw new Error('Company name needs at least one letter or number.');
  const offset = Number(opts['port-offset'] ?? 0);
  if (!Number.isInteger(offset) || offset < 0 || offset > 7000) throw new Error('port-offset must be an integer from 0 to 7000.');
  const temporary = `${destination}.creating-${randomUUID()}`;
  await mkdir(temporary, { recursive: true });
  try {
    const checkout = path.join(temporary, '.companyhq/source');
    const source = opts.source ? (/^https?:|^ssh:|^git@/.test(opts.source) ? opts.source : path.resolve(opts.source)) : defaultSource;
    git(['clone', '--no-checkout', '--', source, checkout], root);
    const branch = opts.branch ?? git(['symbolic-ref', '--short', 'refs/remotes/origin/HEAD'], checkout).replace(/^origin\//, '');
    git(['check-ref-format', `refs/heads/${branch}`], checkout);
    const revision = git(['rev-parse', `refs/remotes/origin/${branch}^{commit}`], checkout);
    git(['checkout', '--detach', revision], checkout);
    for (const required of ['Services/GitHub/GitHub.csproj', 'Services/Widget/Widget.csproj', 'Apps/Api/Integrations/BlogIntegration.cs']) {
      if (!existsSync(path.join(checkout, required))) throw new Error('The selected upstream does not contain the services required by this starter. Publish the current CompanyHQ changes first or select a compatible --source.');
    }
    // Use the installed CLI's matching creation template; generic updates do not reapply it.
    await copy(path.join(root, 'Templates/Company'), temporary);
    await copy(path.join(checkout, 'Apps/Web'), path.join(temporary, 'Apps/Web'));
    await copy(path.join(checkout, 'Assets'), path.join(temporary, 'Assets'));
    // Restore template-specific Docker and Next configuration after the consumer copy.
    for (const file of ['Dockerfile', 'next.config.ts']) await cp(path.join(root, 'Templates/Company/Apps/Web', file), path.join(temporary, 'Apps/Web', file));
    const pkgFile = path.join(temporary, 'Apps/Web/package.json');
    const pkg = await json(pkgFile);
    pkg.name = `@${slug}/web`;
    for (const [key, file] of Object.entries({ 'api:document': 'generate-openapi.mjs', 'api:types': 'generate-api-types.mjs', 'api:check': 'check-openapi.mjs' })) {
      pkg.scripts[key] = `node ../../.companyhq/source/Scripts/${file}${key === 'api:types' ? ' src/api/schema.d.ts' : ''}`;
    }
    await save(pkgFile, pkg);
    const npmLockFile = path.join(temporary, 'Apps/Web/package-lock.json');
    const npmLock = await json(npmLockFile);
    npmLock.name = pkg.name;
    npmLock.packages[''].name = pkg.name;
    await save(npmLockFile, npmLock);
    await save(path.join(temporary, 'project.json'), {
      name: opts.name, slug, ports: { web: 3104 + offset, api: 4102 + offset, database: 55432 + offset, redis: 56379 + offset, smtp: 51025 + offset, mail: 58025 + offset }, apps: { Web: 3104 + offset },
    });
    await save(path.join(temporary, '.companyhq/lock.json'), { version: 1, source, branch, revision });
    if (existsSync(destination)) await rmdir(destination);
    await rename(temporary, destination);
    console.log(`Created ${opts.name} in ${destination}. Start: .\\Scripts\\StartDev.ps1 --Web`);
  } catch (error) {
    await rm(temporary, { recursive: true, force: true });
    throw error;
  }
}
async function update(opts) {
  validateOptions(opts, ['project']);
  const project = path.resolve(opts.project ?? process.cwd());
  const lockFile = path.join(project, '.companyhq/lock.json');
  const lock = await json(lockFile);
  validateLock(lock);
  // Existing sibling consumers can continue referencing their shared local HQ checkout.
  if (lock.mode === 'linked') {
    if (typeof lock.path !== 'string') throw new Error('Linked CompanyHQ requires its saved path.');
    const linked = path.resolve(project, lock.path);
    if (git(['remote', 'get-url', 'origin'], linked) !== lock.source) throw new Error('Linked CompanyHQ origin differs from the saved upstream.');
    if (git(['status', '--porcelain', '--untracked-files=all'], linked)) throw new Error('Linked CompanyHQ has local edits. Commit/publish them before pulling; the project already uses those local services.');
    if (git(['branch', '--show-current'], linked) !== lock.branch) throw new Error('Linked CompanyHQ is not on the saved upstream branch.');
    const previous = git(['rev-parse', 'HEAD'], linked);
    git(['fetch', 'origin', `refs/heads/${lock.branch}`], linked);
    const next = git(['rev-parse', 'FETCH_HEAD^{commit}'], linked);
    if (next === previous) {
      if (lock.revision !== previous) await save(lockFile, { ...lock, revision: previous });
      console.log(`CompanyHQ is already current (${previous}).`); return;
    }
    git(['merge-base', '--is-ancestor', previous, next], linked);
    git(['merge', '--ff-only', next], linked);
    await save(lockFile, { ...lock, revision: next });
    console.log(`Updated linked CompanyHQ ${previous.slice(0, 12)} -> ${next.slice(0, 12)}. Rebuild/restart the project.`);
    return;
  }
  if (lock.mode && lock.mode !== 'managed') throw new Error('Unknown CompanyHQ dependency mode.');
  const checkout = path.join(project, '.companyhq/source');
  if (!existsSync(checkout)) {
    const temporary = path.join(project, `.companyhq/restoring-${randomUUID()}`);
    try {
      git(['clone', '--no-checkout', '--', lock.source, temporary], project);
      git(['checkout', '--detach', lock.revision], temporary);
      await rename(temporary, checkout);
    } catch (error) { await rm(temporary, { recursive: true, force: true }); throw error; }
    console.log(`Restored locked CompanyHQ commit ${lock.revision}. Run Update again to check upstream.`);
    return;
  }
  if (!existsSync(path.join(checkout, '.git'))) throw new Error('The managed dependency is not an independent Git checkout.');
  if (git(['remote', 'get-url', 'origin'], checkout) !== lock.source) throw new Error('Dependency origin differs from the lock file.');
  if (git(['status', '--porcelain', '--untracked-files=all'], checkout)) throw new Error('CompanyHQ has local edits. Move shared changes upstream before updating.');
  const previous = git(['rev-parse', 'HEAD'], checkout);
  if (previous !== lock.revision) throw new Error('Dependency HEAD differs from the lock file.');
  git(['fetch', 'origin', `refs/heads/${lock.branch}`], checkout);
  const next = git(['rev-parse', 'FETCH_HEAD^{commit}'], checkout);
  if (next === previous) { console.log(`CompanyHQ is already current (${previous}).`); return; }
  git(['merge-base', '--is-ancestor', previous, next], checkout);
  git(['checkout', '--detach', next], checkout);
  try { await save(lockFile, { ...lock, revision: next }); }
  catch (error) { git(['checkout', '--detach', previous], checkout); throw error; }
  console.log(`Updated CompanyHQ ${previous.slice(0, 12)} -> ${next.slice(0, 12)}. Rebuild/restart the project; company-owned files were preserved.`);
}
try {
  const [command, ...args] = process.argv.slice(2);
  if (command === 'create' || command === 'init') await create(options(args));
  else if (command === 'update') await update(options(args));
  else {
    console.log('CompanyHQ CLI\n  create (or init) --name "My Company" --path D:/Projects/MyCompany [--source URL] [--branch main] [--port-offset 0]\n  update [--project D:/Projects/MyCompany]');
    if (command && command !== 'help') process.exitCode = 1;
  }
} catch (error) { console.error(error.message); process.exitCode = 1; }
