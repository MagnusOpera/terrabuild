import {existsSync, readdirSync, readFileSync, renameSync, statSync, unlinkSync, writeFileSync} from 'node:fs';
import path from 'node:path';

const root = process.cwd();
const extensionsDir = path.join(root, 'site-docs', 'extensions');

if (!existsSync(extensionsDir)) {
  console.error(`Missing extensions docs directory: ${extensionsDir}`);
  process.exit(1);
}

for (const entry of readdirSync(extensionsDir)) {
  const dirPath = path.join(extensionsDir, entry);
  if (!statSync(dirPath).isDirectory()) {
    continue;
  }

  const legacyIndex = path.join(dirPath, '_index.md');
  const newIndex = path.join(dirPath, 'index.md');

  if (existsSync(legacyIndex)) {
    if (existsSync(newIndex)) {
      unlinkSync(newIndex);
    }

    renameSync(legacyIndex, newIndex);
  }

  for (const file of readdirSync(dirPath)) {
    if (!file.endsWith('.md')) {
      continue;
    }

    const filePath = path.join(dirPath, file);
    const content = readFileSync(filePath, 'utf8')
      .replace(/\(\.\/_index\)/g, '(./index)')
      .replace(/\]\(\/docs\/extensions\/([^/]+)\/_index\)/g, '](/docs/extensions/$1)');
    writeFileSync(filePath, content);
  }
}
