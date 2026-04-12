import {existsSync, readFileSync} from 'node:fs';
import path from 'node:path';
import {spawnSync} from 'node:child_process';

const version = process.argv[2];
if (!version || !/^\d+\.\d+\.\d+$/.test(version)) {
  console.error('Usage: npm run docs:version -- X.Y.Z');
  process.exit(2);
}

const root = process.cwd();
const versionsPath = path.join(root, 'versions.json');

if (existsSync(versionsPath)) {
  const versions = JSON.parse(readFileSync(versionsPath, 'utf8'));
  if (Array.isArray(versions) && versions.includes(version)) {
    console.log(`Docs version ${version} already exists; skipping.`);
    process.exit(0);
  }
}

const bin = process.platform === 'win32'
  ? path.join(root, 'node_modules', '.bin', 'docusaurus.cmd')
  : path.join(root, 'node_modules', '.bin', 'docusaurus');

const result = spawnSync(bin, ['docs:version', version], {
  stdio: 'inherit',
});

if (result.status !== 0) {
  process.exit(result.status ?? 1);
}
