import { readFile } from 'node:fs/promises';
import path from 'node:path';

export const dynamic = 'force-static';

export async function GET() {
  const logo = await readFile(
    path.resolve(process.cwd(), '../../Assets/Logo/logo-frame3.svg'),
  );

  return new Response(logo.toString('utf8'), {
    headers: {
      'Cache-Control': 'public, max-age=31536000, immutable',
      'Content-Type': 'image/svg+xml',
      'X-Content-Type-Options': 'nosniff',
    },
  });
}
