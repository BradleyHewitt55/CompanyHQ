import { readFile } from 'node:fs/promises';
import path from 'node:path';

const iconsDir = path.resolve(process.cwd(), '../../Assets/Icons');

export async function GET(
  _request: Request,
  { params }: { params: Promise<{ file: string }> },
) {
  const { file } = await params;
  if (!/^[a-z0-9-]+\.svg$/.test(file)) return new Response('Not found', { status: 404 });

  const resolved = path.resolve(iconsDir, file);
  if (!resolved.startsWith(iconsDir + path.sep)) return new Response('Not found', { status: 404 });

  try {
    const icon = await readFile(resolved, 'utf8');
    return new Response(icon, {
      headers: {
        'Cache-Control': 'public, max-age=31536000, immutable',
        'Content-Type': 'image/svg+xml',
        'X-Content-Type-Options': 'nosniff',
      },
    });
  } catch {
    return new Response('Not found', { status: 404 });
  }
}
