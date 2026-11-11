#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$script_dir/../.." && pwd)"
website_dir="$repo_root/website"

release_tags=()
previous_line=""
while IFS= read -r tag; do
  if [[ ! "$tag" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] ||
     [[ "${tag%.*}" == "$previous_line" ]] ||
     ! git -C "$repo_root" cat-file -e "$tag:website" 2>/dev/null; then
    continue
  fi
  release_tags+=("$tag")
  previous_line="${tag%.*}"
  if (( ${#release_tags[@]} == 5 )); then
    break
  fi
done < <(git -C "$repo_root" tag --merged HEAD --list --sort=-version:refname)

if (( ${#release_tags[@]} > 0 )) && [[ ! -d "$website_dir/node_modules" ]]; then
  echo "website/node_modules is required before preparing released documentation." >&2
  exit 1
fi

temp_dir="$(mktemp -d "${TMPDIR:-/tmp}/terrabuild-doc-version.XXXXXX")"
trap 'rm -rf "$temp_dir"' EXIT

for tag in "${release_tags[@]}"; do
  release_line="${tag%.*}"
  archive_dir="$temp_dir/$release_line"
  mkdir "$archive_dir"
  git -C "$repo_root" archive "$tag" | tar -x -C "$archive_dir"
  ln -s "$website_dir/node_modules" "$archive_dir/website/node_modules"

  "$script_dir/generate-whats-new.sh" "$tag" "$archive_dir/CHANGELOG.md" \
    "$archive_dir/website/site-docs/whats-new.md"
  case "$tag" in
    0.204.1|0.205.3|0.206.0|0.207.3|0.208.0)
      # These snapshots predate the .NET 11 nullability fixes.
      NoWarn="${NoWarn:+$NoWarn;}3261;3262" make -C "$archive_dir" docs
      ;;
    *) make -C "$archive_dir" docs ;;
  esac

  (
    cd "$archive_dir/website"
    ./node_modules/.bin/docusaurus docs:version "$release_line"
  )
  echo "Documentation for $release_line generated from $tag."
done

node - "$repo_root" "$temp_dir" "${release_tags[@]}" <<'NODE'
const {execFileSync} = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');

const [repoRoot, tempDir, ...tags] = process.argv.slice(2);
const websiteDir = path.join(repoRoot, 'website');
const docsDir = path.join(websiteDir, 'versioned_docs');
const sidebarsDir = path.join(websiteDir, 'versioned_sidebars');
const releaseLines = tags.map((tag) => tag.slice(0, tag.lastIndexOf('.')));

fs.mkdirSync(docsDir, {recursive: true});
fs.mkdirSync(sidebarsDir, {recursive: true});
for (const line of releaseLines) {
  const source = path.join(tempDir, line, 'website');
  const snapshot = `version-${line}`;
  const destination = path.join(docsDir, snapshot);
  fs.rmSync(destination, {recursive: true, force: true});
  fs.cpSync(path.join(source, 'versioned_docs', snapshot), destination, {recursive: true});
  const sidebar = `${snapshot}-sidebars.json`;
  fs.copyFileSync(path.join(source, 'versioned_sidebars', sidebar), path.join(sidebarsDir, sidebar));
}

// Keep historical routes from Git while excluding stale generated snapshots.
const historicalVersions = execFileSync('git', [
  '-C', repoRoot, 'ls-files', '-z', '--', 'website/versioned_sidebars/',
], {encoding: 'utf8'}).split('\0')
  .map((file) => /^version-(\d+\.\d+\.\d+)-sidebars\.json$/.exec(path.basename(file))?.[1])
  .filter((version) => version && fs.existsSync(path.join(docsDir, `version-${version}`)))
  .sort((left, right) => right.localeCompare(left, 'en', {numeric: true}));

const versions = [...releaseLines, ...historicalVersions];
fs.writeFileSync(path.join(websiteDir, 'versions.json'), `${JSON.stringify(versions, null, 2)}\n`);
NODE
