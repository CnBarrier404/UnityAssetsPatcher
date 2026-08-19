import { copyFile } from 'node:fs/promises';

await copyFile(
  new URL('../../schema/schema-v1.json', import.meta.url),
  new URL('../dist/schema-v1.json', import.meta.url),
);
