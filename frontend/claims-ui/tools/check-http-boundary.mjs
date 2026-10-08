import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';

const appRoot = join(process.cwd(), 'src', 'app');
const allowedSegments = [join('core', 'services'), join('core', 'interceptors')];

function files(path) {
  return readdirSync(path).flatMap((name) => {
    const candidate = join(path, name);
    return statSync(candidate).isDirectory() ? files(candidate) : [candidate];
  });
}

const violations = files(appRoot)
  .filter((file) => file.endsWith('.ts') && !file.endsWith('.spec.ts'))
  .filter((file) => /\bHttpClient\b/.test(readFileSync(file, 'utf8')))
  .map((file) => relative(appRoot, file))
  .filter((file) => !allowedSegments.some((segment) => file.startsWith(segment)));

if (violations.length > 0) {
  console.error(`HttpClient is restricted to the service layer:\n${violations.join('\n')}`);
  process.exit(1);
}

console.log('HTTP boundary check passed.');
