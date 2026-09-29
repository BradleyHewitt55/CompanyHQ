import 'server-only';

import { mkdir, readFile, readdir, rename, unlink, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { randomUUID } from 'node:crypto';

export const companyLogoDirectory = path.resolve(process.cwd(), '../../Assets/Logo');
export const maxCompanyLogoBytes = 5 * 1024 * 1024;
export type CompanyLogoExtension = 'jpg' | 'png' | 'svg' | 'webp';

export const companyLogoUrl = (extension: CompanyLogoExtension) => `/company-logo/company-logo.${extension}`;

const contentTypes = {
  'image/jpeg': 'jpg',
  'image/png': 'png',
  'image/svg+xml': 'svg',
  'image/webp': 'webp',
} as const;

const contentTypesByExtension: Record<CompanyLogoExtension, string> = {
  jpg: 'image/jpeg',
  png: 'image/png',
  svg: 'image/svg+xml',
  webp: 'image/webp',
};

const logoFilePattern = /^company-logo\.(?:jpg|png|svg|webp)$/;

export type SavedCompanyLogo = {
  path: string;
  previousBytes: Buffer | null;
  url: string;
};

export async function saveCompanyLogo(file: File): Promise<SavedCompanyLogo> {
  const extension = contentTypes[file.type as keyof typeof contentTypes];
  if (!extension) {
    throw new Error('Choose an SVG, PNG, JPEG, or WebP logo.');
  }
  if (file.size <= 0 || file.size > maxCompanyLogoBytes) {
    throw new Error('Choose a logo smaller than 5 MB.');
  }

  const bytes = Buffer.from(await file.arrayBuffer());
  if (bytes.length !== file.size) {
    throw new Error('The logo upload could not be read. Try again.');
  }

  await mkdir(companyLogoDirectory, { recursive: true });
  const target = path.join(companyLogoDirectory, `company-logo.${extension}`);
  const previousBytes = await readFile(target).catch(() => null);
  const temporary = path.join(companyLogoDirectory, `company-logo.${extension}.${randomUUID()}.upload`);

  try {
    await writeFile(temporary, bytes);
    await rename(temporary, target);
  } catch (error) {
    await unlink(temporary).catch(() => {});
    throw error;
  }
  return { path: target, previousBytes, url: companyLogoUrl(extension) };
}

export async function rollbackCompanyLogo(saved: SavedCompanyLogo) {
  if (saved.previousBytes) {
    await writeFile(saved.path, saved.previousBytes);
  } else {
    await unlink(saved.path).catch(() => {});
  }
}

export async function removeCompanyLogoFiles(keepPath?: string) {
  const entries = await readdir(companyLogoDirectory, { withFileTypes: true }).catch(() => []);
  await Promise.all(entries
    .filter(entry => entry.isFile() && logoFilePattern.test(entry.name))
    .map(entry => {
      const filePath = path.join(companyLogoDirectory, entry.name);
      return filePath === keepPath ? Promise.resolve() : unlink(filePath).catch(() => {});
    }));
}

export async function getCompanyLogoFile(extension?: CompanyLogoExtension) {
  const entries = await readdir(companyLogoDirectory, { withFileTypes: true }).catch(() => []);
  const candidates = entries
    .filter(entry => entry.isFile() && logoFilePattern.test(entry.name)
      && (!extension || entry.name === `company-logo.${extension}`))
    .map(entry => path.join(companyLogoDirectory, entry.name));

  for (const filePath of candidates) {
    try {
      const bytes = await readFile(filePath);
      const extension = path.extname(filePath).slice(1);
      return { bytes, contentType: contentTypesByExtension[extension as CompanyLogoExtension] };
    } catch {
      // A concurrent replacement can remove a candidate between readdir and readFile.
    }
  }

  return null;
}
