import { getCompanyLogoFile, type CompanyLogoExtension } from '../../../lib/company-logo';

export const dynamic = 'force-dynamic';

export async function GET(
  _request: Request,
  { params }: { params: Promise<{ file: string }> },
) {
  const { file } = await params;
  const match = /^company-logo\.(jpg|png|svg|webp)$/.exec(file);
  if (!match) return new Response('Not found', { status: 404 });

  const logo = await getCompanyLogoFile(match[1] as CompanyLogoExtension);
  if (!logo) return new Response('Not found', { status: 404 });

  return new Response(new Uint8Array(logo.bytes), {
    headers: {
      'Cache-Control': 'no-store',
      'Content-Type': logo.contentType,
      'X-Content-Type-Options': 'nosniff',
    },
  });
}
