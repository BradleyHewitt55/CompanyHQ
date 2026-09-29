import { FlatCompat } from '@eslint/eslintrc';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const compat = new FlatCompat({ baseDirectory: path.dirname(fileURLToPath(import.meta.url)) });
const config = [
  { ignores: ['.next/**', 'node_modules/**', 'src/api/schema.d.ts', 'next-env.d.ts', 'tests/**', 'playwright.config.ts', 'test-results/**', 'playwright-report/**'] },
  ...compat.extends('next/core-web-vitals', 'next/typescript'),
  { rules: { '@next/next/no-img-element': 'off', '@typescript-eslint/no-unused-vars': ['warn', { argsIgnorePattern: '^_' }] } },
];
export default config;
