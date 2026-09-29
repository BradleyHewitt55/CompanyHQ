// Shared by the OpenAPI scripts. The API host is the only source of truth: its controllers are
// built into one document per plugged-in service (see Apps/Api/OpenApi/ServiceOpenApiDocuments.cs),
// and nothing here lists services by hand.
import { execFileSync } from 'node:child_process';
import { existsSync, mkdtempSync, readdirSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

export const root = join(dirname(fileURLToPath(import.meta.url)), '..');

/**
 * Builds the API with document emission on and returns { name: json } for every service document.
 * The host boots to describe itself, so the usual .env settings apply; migrations and seeding are
 * switched off because describing the API must never touch a database.
 */
export function buildDocuments() {
  const out = mkdtempSync(join(tmpdir(), 'companyhq-openapi-'));
  try {
    execFileSync(
      'dotnet',
      [
        'build',
        join(root, 'Apps', 'Api', 'Api.csproj'),
        '-nologo',
        '-v', 'q',
        // An up-to-date build skips emission, which would look like "no services".
        '--no-incremental',
        '-p:OpenApiGenerateDocumentsOnBuild=true',
        `-p:OpenApiDocumentsDirectory=${out}`,
      ],
      { stdio: 'inherit', env: { ...process.env, Postgres__AutoMigrate: 'false', Postgres__AutoSeed: 'false' } },
    );

    const documents = {};
    for (const file of readdirSync(out)) {
      const match = /^Api_(.+)\.json$/.exec(file);
      if (match) documents[match[1]] = readFileSync(join(out, file), 'utf8');
    }
    if (Object.keys(documents).length === 0) throw new Error('The API build emitted no OpenAPI documents.');
    return documents;
  } finally {
    rmSync(out, { recursive: true, force: true });
  }
}

/** A document named `faq` belongs to `Services/Faq/Contracts/OpenApi/faq.json`. */
export function documentPath(name) {
  const folder = join(root, 'Services', name[0].toUpperCase() + name.slice(1));
  if (!existsSync(folder)) throw new Error(`No service folder for OpenAPI document '${name}' (expected ${folder}).`);
  return join(folder, 'Contracts', 'OpenApi', `${name}.json`);
}

/** Every committed service document, found by folder convention. */
export function committedDocuments() {
  const services = join(root, 'Services');
  const documents = {};
  for (const folder of readdirSync(services, { withFileTypes: true })) {
    if (!folder.isDirectory()) continue;
    const dir = join(services, folder.name, 'Contracts', 'OpenApi');
    if (!existsSync(dir)) continue;
    for (const file of readdirSync(dir).filter((name) => name.endsWith('.json'))) {
      documents[file.slice(0, -'.json'.length)] = readFileSync(join(dir, file), 'utf8');
    }
  }
  return documents;
}
