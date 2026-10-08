import { writeFileSync } from 'node:fs';

const rawUrl = process.env.API_BASE_URL;
if (!rawUrl) {
  throw new Error('API_BASE_URL must be set for a production build.');
}

const apiBaseUrl = rawUrl.replace(/\/+$/, '');
const parsed = new URL(apiBaseUrl);
if (!['http:', 'https:'].includes(parsed.protocol)) {
  throw new Error('API_BASE_URL must use http or https.');
}

writeFileSync(
  new URL('../src/environments/environment.prod.ts', import.meta.url),
  `export const environment = {\n  production: true,\n  apiBaseUrl: ${JSON.stringify(apiBaseUrl)}\n} as const;\n`,
  'utf8'
);

