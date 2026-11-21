#!/usr/bin/env node
// Run after website dependencies are installed. No releases are downloaded or published.
import assert from 'node:assert/strict';
import {execFileSync} from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const require = createRequire(fs.realpathSync(path.join(root, 'website/node_modules/@docusaurus/core/package.json')));
const yaml = require('js-yaml');
const workflow = (name) => yaml.load(fs.readFileSync(path.join(root, `.github/workflows/${name}.yml`), 'utf8'));
const prepare = workflow('on-push-tag');
const published = workflow('on-release-published');
const homebrew = workflow('release-homebrew-tap');
const step = (flow, job, name) => flow.jobs[job].steps.find(s => s.name === name).run;
const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'terrabuild-release-tags-'));
const run = (script, env) => execFileSync('bash', ['-euo', 'pipefail', '-c', script], {
  cwd: root, env: {...process.env, ...env}, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'],
});
try {
  const envFile = path.join(temp, 'env');
  const bin = path.join(temp, 'bin');
  fs.mkdirSync(bin);
  fs.writeFileSync(path.join(bin, 'dotnet'), '#!/usr/bin/env bash\nprintf "%s\\n" "$@"\n', {mode: 0o755});
  fs.writeFileSync(path.join(bin, 'gh'), `#!/usr/bin/env bash
set -euo pipefail
[[ "$*" == *"$EXPECTED_TAG"* ]]
for platform in darwin-arm64 linux-x64 linux-arm64; do
  printf fixture > "terrabuild-$EXPECTED_VERSION-$platform.zip"
done
`, {mode: 0o755});
  const env = {PATH: `${bin}:${process.env.PATH}`, GITHUB_ENV: envFile, NUGET_API_KEY: 'fixture'};
  assert.deepEqual(prepare.on.push.tags, ['v[0-9]*.[0-9]*.[0-9]*']);
  for (const tag of ['v0.209.0', 'v0.209.1-next']) {
    fs.writeFileSync(envFile, '');
    run(step(prepare, 'build-and-release', 'Validate release tag'), {...env, GITHUB_REF_NAME: tag});
    const version = tag.slice(1);
    assert(fs.readFileSync(envFile, 'utf8').includes(`BUILD_VERSION=${version}\n`));
    const suffix = fs.readFileSync(envFile, 'utf8').split('\n').find(s => s.startsWith('BUILD_VERSION_SUFFIX=')).split('=')[1];
    assert.equal(Boolean(suffix), tag.endsWith('-next'));
  }
  for (const tag of ['0.209.0', 'v0.209.0-beta', 'v01.209.0', 'website-0.209.0']) {
    assert.throws(() => run(step(prepare, 'build-and-release', 'Validate release tag'), {...env, GITHUB_REF_NAME: tag}));
  }
  // Legacy releases remain retryable; all packages and archives use numeric versions.
  for (const tag of ['0.208.0', 'v0.209.0', 'v0.209.1-next']) {
    const version = tag.replace(/^v/, '');
    const args = run(step(published, 'release-nuget', 'NuGet Release'), {...env, RELEASE_TAG: tag});
    assert(args.includes(`Terrabuild.${version}.nupkg\n`));
    fs.writeFileSync(envFile, '');
    run(step(published, 'sign-and-notarize', 'Resolve artifact version'), {...env, RELEASE_TAG: tag});
    assert.equal(fs.readFileSync(envFile, 'utf8'), `BUILD_VERSION=${version}\n`);
    fs.writeFileSync(envFile, '');
    run(step(homebrew, 'update-homebrew-tap', 'Prepare formula name'), {...env, TERRABUILD_VERSION: tag});
    assert(fs.readFileSync(envFile, 'utf8').includes(`TERRABUILD_FORMULA_FILE=terrabuild${tag.endsWith('-next') ? '-next' : ''}\n`));
    const formula = run('bash .github/scripts/generate-homebrew-tap "$EXPECTED_TAG" .github/scripts/terrabuild-formula-template.rb', {
      ...env, EXPECTED_TAG: tag, EXPECTED_VERSION: version,
    });
    assert(formula.includes(`version "${version}"`));
    assert(formula.includes(`/download/${tag}/terrabuild-${version}-darwin-arm64.zip`));
  }
  const fixture = path.join(temp, 'notes');
  fs.mkdirSync(fixture);
  fs.writeFileSync(path.join(fixture, 'CHANGELOG.md'), '## [0.209.0]\n\n- Release feature.\n\n**Full Changelog**: https://example.com/compare/0.208.0...v0.209.0\n');
  const notes = execFileSync('bash', [path.join(root, '.github/scripts/extract-release-notes.sh'), 'v0.209.0'], {cwd: fixture, encoding: 'utf8'});
  assert(notes.includes('- Release feature.'));
  console.log('Release tag workflow, package, Homebrew, and release-note tests passed.');
} finally {
  fs.rmSync(temp, {recursive: true, force: true});
}
