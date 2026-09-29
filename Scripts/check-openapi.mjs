// npm run api:check — fail when a committed service document no longer matches the controllers.
import { buildDocuments, committedDocuments } from './openapi.mjs';

const generated = buildDocuments();
const committed = committedDocuments();
const names = new Set([...Object.keys(generated), ...Object.keys(committed)]);
const stale = [...names].filter((name) => generated[name] !== committed[name]);

if (stale.length > 0) {
  console.error(`OpenAPI documents out of date: ${stale.join(', ')}. Run npm run api:document.`);
  process.exit(1);
}

console.log(`OpenAPI documents match the API (${names.size}).`);
