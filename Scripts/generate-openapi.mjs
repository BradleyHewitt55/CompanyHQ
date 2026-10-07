// npm run api:document — regenerate every service's Contracts/OpenApi/{name}.json.
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';

import { buildDocuments, committedDocuments, documentPath } from './openapi.mjs';

const project = process.argv[2] ? resolve(process.argv[2]) : undefined;
const services = process.argv[3] ? resolve(process.argv[3]) : undefined;
const documents = buildDocuments(project);

// A service that is no longer plugged in no longer has a document.
for (const name of Object.keys(committedDocuments(services))) {
  if (!(name in documents)) rmSync(documentPath(name, services));
}

for (const [name, json] of Object.entries(documents)) {
  const target = documentPath(name, services);
  mkdirSync(dirname(target), { recursive: true });
  writeFileSync(target, json);
  console.log(`wrote ${name}`);
}
