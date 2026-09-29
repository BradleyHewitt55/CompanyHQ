// npm run api:types -- <output.d.ts>
// Merges every committed service document into one OpenAPI document and generates TypeScript from
// it, so an app sees every plugged-in service's shapes without listing services anywhere.
import { execSync } from 'node:child_process';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

import { committedDocuments } from './openapi.mjs';

const output = process.argv[2];
if (!output) {
  console.error('Usage: node generate-api-types.mjs <output.d.ts>');
  process.exit(1);
}

const merged = { openapi: '3.1.1', info: { title: 'CompanyHQ', version: '1.0.0' }, paths: {}, components: { schemas: {} } };

for (const [name, json] of Object.entries(committedDocuments()).sort(([a], [b]) => a.localeCompare(b))) {
  const document = JSON.parse(json);
  for (const [path, item] of Object.entries(document.paths ?? {})) {
    if (path in merged.paths) throw new Error(`Route ${path} is published by more than one service (${name}).`);
    merged.paths[path] = item;
  }
  for (const [schema, definition] of Object.entries(document.components?.schemas ?? {})) {
    const existing = merged.components.schemas[schema];
    // Shared shapes (PagedResult, ProblemDetails, ...) appear in several documents; they must agree.
    if (existing && JSON.stringify(existing) !== JSON.stringify(definition)) {
      throw new Error(`Schema ${schema} differs between service documents (${name}).`);
    }
    merged.components.schemas[schema] = definition;
  }
}

const work = mkdtempSync(join(tmpdir(), 'companyhq-types-'));
try {
  const input = join(work, 'merged.json');
  writeFileSync(input, JSON.stringify(merged));
  // Resolved from the calling app's own node_modules.
  execSync(`npx --no-install openapi-typescript "${input}" -o "${resolve(output)}"`, { stdio: 'inherit' });
} finally {
  rmSync(work, { recursive: true, force: true });
}
