// npm run api:types — from the running API container, refresh every company service's OpenAPI
// contract and regenerate src/api/schema.d.ts. The documents were written when the API image was
// built, so they always describe the C# contracts that are actually running.
import { execFileSync, execSync, spawnSync } from 'node:child_process';
import { copyFileSync, mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const web = dirname(fileURLToPath(import.meta.url));
const root = join(web, '../..');
const services = join(root, 'Services');
const { slug } = JSON.parse(readFileSync(join(root, 'project.json'), 'utf8'));
const out = mkdtempSync(join(tmpdir(), 'company-openapi-'));
try {
  // Whichever stack is up: the development one, or failing that production.
  const container = [`${slug}-dev-api-1`, `${slug}-api-1`]
    .find(name => spawnSync('docker', ['ps', '-q', '--filter', `name=^${name}$`], { encoding: 'utf8' }).stdout.trim());
  if (!container) throw new Error('The API is not running. Start the stack first.');
  execFileSync('docker', ['cp', `${container}:/app/openapi/.`, out], { stdio: 'inherit' });
  let everything;
  for (const file of readdirSync(out)) {
    const name = /_(.+)\.json$/.exec(file)?.[1];
    if (!name) continue;
    // "api" describes every controller; any other document belongs to the service folder of that name.
    if (name === 'api') { everything = join(out, file); continue; }
    const folder = readdirSync(services).find(entry => entry.toLowerCase() === name);
    if (!folder) throw new Error(`No Services folder for OpenAPI contract '${name}'.`);
    const target = join(services, folder, 'Contracts', 'OpenApi');
    mkdirSync(target, { recursive: true });
    copyFileSync(join(out, file), join(target, `${name}.json`));
    console.log(`wrote ${name}`);
  }
  if (!everything) throw new Error('The API image holds no OpenAPI document. Start the stack first.');
  execSync(`npx --no-install openapi-typescript "${everything}" -o "${join(web, 'src/api/schema.d.ts')}"`, { cwd: web, stdio: 'inherit' });
} finally {
  rmSync(out, { recursive: true, force: true });
}
