// npm run api:document — regenerate every service's Contracts/OpenApi/{name}.json.
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';

import { buildDocuments, committedDocuments, documentPath } from './openapi.mjs';

const documents = buildDocuments();

// A service that is no longer plugged in no longer has a document.
for (const name of Object.keys(committedDocuments())) {
  if (!(name in documents)) rmSync(documentPath(name));
}

for (const [name, json] of Object.entries(documents)) {
  const target = documentPath(name);
  mkdirSync(dirname(target), { recursive: true });
  writeFileSync(target, json);
  console.log(`wrote ${name}`);
}
