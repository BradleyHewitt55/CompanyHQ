#!/usr/bin/env node
import { cp, mkdir, readFile, writeFile, readdir, rename, rm, rmdir } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { randomUUID } from 'node:crypto';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
// Every CompanyHQ package shares one version. The generic Rss package is published after the other
// generic packages, so its feed lists the complete releases; CompanyHQ.Api is deliberately not read.
async function latestRelease() {
  let token = process.env.COMPANYHQ_PACKAGES_TOKEN?.trim();
  if (!token) {
    const result = spawnSync('gh', ['auth', 'token'], { encoding: 'utf8', windowsHide: true });
    if (result.status === 0) token = result.stdout.trim();
  }
  if (!token) throw new Error('Set COMPANYHQ_PACKAGES_TOKEN to a GitHub token with read:packages, or sign in with the GitHub CLI.');
  const config = await readFile(path.join(root, 'Templates/Company/nuget.config'), 'utf8');
  const feed = /<add key="companyhq" value="(https:\/\/[^"<>]+)\/index\.json"\s*\/>/.exec(config)?.[1];
  if (!feed) throw new Error('nuget.config has no companyhq package source.');
  let response;
  try {
    response = await fetch(`${feed}/download/companyhq.services.rss/index.json`, {
      headers: { Authorization: `Basic ${Buffer.from(`token:${token}`).toString('base64')}` },
      signal: AbortSignal.timeout(30000),
    });
  } catch { throw new Error('Could not read the CompanyHQ package feed.'); }
  if (!response.ok) throw new Error(`CompanyHQ package feed returned HTTP ${response.status}. Check read:packages access.`);
  let data;
  try { data = await response.json(); } catch { throw new Error('CompanyHQ package feed returned invalid JSON.'); }
  // Match Update.ps1: stable numeric versions, ordered by their version components.
  const versions = (Array.isArray(data.versions) ? data.versions : [])
    .filter(version => typeof version === 'string' && /^\d+(\.\d+){1,3}$/.test(version))
    .sort((a, b) => {
      const left = a.split('.').map(BigInt), right = b.split('.').map(BigInt);
      for (let i = 0; i < 4; i++) {
        const difference = (right[i] ?? -1n) - (left[i] ?? -1n);
        if (difference) return difference > 0n ? 1 : -1;
      }
      return 0;
    });
  if (!versions.length) throw new Error('No released CompanyHQ version is published.');
  return versions[0];
}
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
const ignored = new Set(['node_modules', 'bin', 'obj', '.git', '.env', '.env.dev', '.env.local']);
async function copy(source, destination) {
  await cp(source, destination, { recursive: true, filter: file => !ignored.has(path.basename(file)) });
}
async function create(opts) {
  validateOptions(opts, ['name', 'path', 'port-offset']);
  if (!opts.name) throw new Error('create requires --name.');
  // Without --path the company gets a folder named after it in the current directory.
  const destination = path.resolve(opts.path ?? opts.name.replace(/[^A-Za-z0-9]+/g, ''));
  if (existsSync(destination) && (await readdir(destination)).length) throw new Error('Create requires an empty destination; existing projects are never overwritten.');
  const slug = opts.name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
  if (!slug) throw new Error('Company name needs at least one letter or number.');
  const offset = Number(opts['port-offset'] ?? 0);
  if (!Number.isInteger(offset) || offset < 0 || offset > 7000) throw new Error('port-offset must be an integer from 0 to 7000.');
  const temporary = `${destination}.creating-${randomUUID()}`;
  await mkdir(temporary, { recursive: true });
  try {
    // The starter is only the API template; any web app or assets are the company's own to add.
    await copy(path.join(root, 'Templates/Company'), temporary);
    await save(path.join(temporary, 'project.json'), { name: opts.name, slug, ports: { api: 4102 + offset }, apps: {} });
    // npm never packs a file named .gitignore, so the starter carries it under another name.
    await rename(path.join(temporary, 'gitignore'), path.join(temporary, '.gitignore'));
    // The development values file, and a production one with the same keys left empty.
    const development = await readFile(path.join(temporary, 'env.dev'), 'utf8');
    await rename(path.join(temporary, 'env.dev'), path.join(temporary, '.env.dev'));
    await writeFile(path.join(temporary, '.env'), development
      .replace(/^# Development values, read by StartDev\./m, '# Production values, read by StartProd.')
      .replace(/^# Production values go in \.env, read by StartProd;/m, '# Development values go in .env.dev, read by StartDev;')
      .replace(/^([A-Za-z_][A-Za-z0-9_]*)=.*$/gm, '$1='));
    // A tag can precede (or outlive a failed) publish; select the package feed as Update does.
    const release = await latestRelease();
    const propsFile = path.join(temporary, 'Directory.Build.props');
    await writeFile(propsFile, (await readFile(propsFile, 'utf8')).replace(/<CompanyHQVersion>[^<]+</, `<CompanyHQVersion>${release}<`));
    if (existsSync(destination)) await rmdir(destination);
    await rename(temporary, destination);
    console.log(`Created ${opts.name} in ${destination}. Sign in to the GitHub CLI or set COMPANYHQ_PACKAGES_TOKEN, then run .\\Scripts\\StartDev.ps1`);
  } catch (error) {
    await rm(temporary, { recursive: true, force: true });
    throw error;
  }
}
try {
  const [command, ...args] = process.argv.slice(2);
  if (command === 'create' || command === 'init') await create(options(args));
  else {
    console.log('CompanyHQ CLI\n  create (or init) --name "My Company" [--path D:/Projects/MyCompany] [--port-offset 0]');
    if (command && command !== 'help') process.exitCode = 1;
  }
} catch (error) { console.error(error.message); process.exitCode = 1; }
