#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$script_dir/../.." && pwd)"
website_dir="$repo_root/website"

latest_stable=""
while IFS= read -r tag; do
  if [[ "$tag" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] &&
     git -C "$repo_root" cat-file -e "$tag:website" 2>/dev/null; then
    latest_stable="$tag"
    break
  fi
done < <(git -C "$repo_root" tag --list --sort=-creatordate)

if [[ -z "$latest_stable" ]]; then
  echo "No stable application tag found; skipping released documentation snapshot."
  exit 0
fi

snapshot_dir="$website_dir/versioned_docs/version-$latest_stable"
snapshot_sidebar="$website_dir/versioned_sidebars/version-$latest_stable-sidebars.json"

if [[ ! -d "$snapshot_dir" || ! -f "$snapshot_sidebar" ]]; then
  if [[ ! -d "$website_dir/node_modules" ]]; then
    echo "website/node_modules is required before preparing released documentation." >&2
    exit 1
  fi

  temp_dir="$(mktemp -d "${TMPDIR:-/tmp}/terrabuild-doc-version.XXXXXX")"
  trap 'rm -rf "$temp_dir"' EXIT

  git -C "$repo_root" archive "$latest_stable" | tar -x -C "$temp_dir"
  ln -s "$website_dir/node_modules" "$temp_dir/website/node_modules"

  # Release pages are generated locally rather than committed in release tags.
  "${script_dir}/generate-whats-new.sh" "$latest_stable" "$temp_dir/CHANGELOG.md" \
    "$temp_dir/website/site-docs/whats-new.md" "$latest_stable"
  make -C "$temp_dir" docs

  (
    cd "$temp_dir/website"
    ./node_modules/.bin/docusaurus docs:version "$latest_stable"
  )

  mkdir -p "$website_dir/versioned_docs" "$website_dir/versioned_sidebars"
  cp -R "$temp_dir/website/versioned_docs/version-$latest_stable" "$snapshot_dir"
  cp "$temp_dir/website/versioned_sidebars/version-$latest_stable-sidebars.json" "$snapshot_sidebar"
fi

node - "$website_dir" "$latest_stable" <<'NODE'
const fs = require('node:fs');
const path = require('node:path');

const [websiteDir, latestStable] = process.argv.slice(2);
const versionsPath = path.join(websiteDir, 'versions.json');
// Rebuild the registry from complete snapshots, including historical tracked
// versions, so a fresh checkout does not need a generated versions.json.
const versions = fs.readdirSync(path.join(websiteDir, 'versioned_sidebars'))
  .map((file) => /^version-(\d+\.\d+\.\d+)-sidebars\.json$/.exec(file)?.[1])
  .filter((version) => version && fs.existsSync(path.join(websiteDir, 'versioned_docs', `version-${version}`)))
  .sort((left, right) => right.localeCompare(left, 'en', {numeric: true}));
const updated = [latestStable, ...versions.filter((version) => version !== latestStable)];

fs.writeFileSync(versionsPath, `${JSON.stringify(updated, null, 2)}\n`);
NODE

echo "Released documentation version: $latest_stable"
