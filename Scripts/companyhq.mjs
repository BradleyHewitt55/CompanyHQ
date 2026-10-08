#!/usr/bin/env node
import { cp, mkdir, readFile, writeFile, readdir, rename, rm, rmdir } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { randomUUID } from 'node:crypto';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const repository = 'https://github.com/BradleyHewitt55/CompanyHQ.git';
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
const ignored = new Set(['node_modules', '.next', 'bin', 'obj', '.git', 'artifacts', '.env', '.env.dev', '.env.local', 'tsconfig.tsbuildinfo', 'playwright.config.ts']);
async function copy(source, destination) {
  await cp(source, destination, { recursive: true, filter: file => !ignored.has(path.basename(file)) });
}
async function create(opts) {
  validateOptions(opts, ['name', 'path', 'port-offset']);
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
    // The starter website and assets are copied once; from then on the company owns them.
    await copy(path.join(root, 'Templates/Company'), temporary);
    await copy(path.join(root, 'Apps/Web'), path.join(temporary, 'Apps/Web'));
    await copy(path.join(root, 'Assets'), path.join(temporary, 'Assets'));
    // Restore template-specific Docker, Next and type-generation files after the website copy.
    await copy(path.join(root, 'Templates/Company/Apps/Web'), path.join(temporary, 'Apps/Web'));
    const pkgFile = path.join(temporary, 'Apps/Web/package.json');
    const pkg = await json(pkgFile);
    pkg.name = `@${slug}/web`;
    for (const key of ['api:document', 'api:sync', 'api:check']) delete pkg.scripts[key];
    pkg.scripts['api:types'] = 'node api-types.mjs';
    await save(pkgFile, pkg);
    const npmLockFile = path.join(temporary, 'Apps/Web/package-lock.json');
    const npmLock = await json(npmLockFile);
    npmLock.name = pkg.name;
    npmLock.packages[''].name = pkg.name;
    await save(npmLockFile, npmLock);
    await save(path.join(temporary, 'project.json'), {
      name: opts.name, slug, ports: { web: 3104 + offset, api: 4102 + offset, database: 55432 + offset, redis: 56379 + offset, smtp: 51025 + offset, mail: 58025 + offset }, apps: { Web: 3104 + offset },
    });
    // npm never packs a file named .gitignore, so the starter carries it under another name.
    await rename(path.join(temporary, 'gitignore'), path.join(temporary, '.gitignore'));
    // The development values file, and a production one with the same keys left empty.
    const development = await readFile(path.join(temporary, 'env.dev'), 'utf8');
    await rename(path.join(temporary, 'env.dev'), path.join(temporary, '.env.dev'));
    await writeFile(path.join(temporary, '.env'), development
      .replace(/^# Development values, read by StartDev\./m, '# Production values, read by StartProd.')
      .replace(/^# Production values go in \.env, read by StartProd;/m, '# Development values go in .env.dev, read by StartDev;')
      .replace(/^([A-Za-z_][A-Za-z0-9_]*)=.*$/gm, '$1='));
    // Pin the newest published release; the company's Update script moves it afterward. This also
    // runs from an npx download, which is not a Git checkout, so the tags are read from GitHub.
    const release = git(['ls-remote', '--tags', '--refs', repository, 'v*'], temporary).split('\n')
      .map(line => /refs\/tags\/v(\d+)\.(\d+)\.(\d+)$/.exec(line)?.slice(1).map(Number))
      .filter(Boolean)
      .sort((a, b) => b[0] - a[0] || b[1] - a[1] || b[2] - a[2])[0]?.join('.');
    if (release) {
      const propsFile = path.join(temporary, 'Directory.Build.props');
      await writeFile(propsFile, (await readFile(propsFile, 'utf8')).replace(/<CompanyHQVersion>[^<]+</, `<CompanyHQVersion>${release}<`));
    }
    if (existsSync(destination)) await rmdir(destination);
    await rename(temporary, destination);
    console.log(`Created ${opts.name} in ${destination}. Fill in .env.dev, then run .\\Scripts\\StartDev.ps1`);
  } catch (error) {
    await rm(temporary, { recursive: true, force: true });
    throw error;
  }
}
try {
  const [command, ...args] = process.argv.slice(2);
  if (command === 'create' || command === 'init') await create(options(args));
  else {
    console.log('CompanyHQ CLI\n  create (or init) --name "My Company" --path D:/Projects/MyCompany [--port-offset 0]');
    if (command && command !== 'help') process.exitCode = 1;
  }
} catch (error) { console.error(error.message); process.exitCode = 1; }
