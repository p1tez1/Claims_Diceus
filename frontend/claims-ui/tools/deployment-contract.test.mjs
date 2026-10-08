import { readFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
import test from 'node:test';

test('the deployment targets the fixed assessment resources without running migrations', async () => {
  const workflow = await readFile(new URL('../../../.github/workflows/deploy.yml', import.meta.url), 'utf8');
  assert.match(workflow, /app-name: diceus/);
  assert.match(workflow, /AZURE_WEBAPP_PUBLISH_PROFILE/);
  assert.match(workflow, /AZURE_STATIC_WEB_APPS_API_TOKEN/);
  assert.match(workflow, /release\/frontend\/claims-ui\/dist\/claims-ui\/browser/);
  assert.doesNotMatch(workflow, /dotnet ef|sql-action|AZURE_SQL_CONNECTION_STRING/i);
});
