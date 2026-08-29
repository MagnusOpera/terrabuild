#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$script_dir/../.." && pwd)"
website_dir="$repo_root/website"

latest_stable=""
while IFS= read -r tag; do
  if [[ "$tag" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    latest_stable="$tag"
    break
  fi
done < <(git -C "$repo_root" tag --list --sort=-version:refname)

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

  make -C "$temp_dir" docs

  (
    cd "$temp_dir/website"
    ./node_modules/.bin/docusaurus docs:version "$latest_stable"
  )

  mkdir -p "$website_dir/versioned_docs" "$website_dir/versioned_sidebars"
  cp -R "$temp_dir/website/versioned_docs/version-$latest_stable" "$snapshot_dir"
  cp "$temp_dir/website/versioned_sidebars/version-$latest_stable-sidebars.json" "$snapshot_sidebar"
fi

node - "$website_dir/versions.json" "$latest_stable" <<'NODE'
const fs = require('node:fs');

const [versionsPath, latestStable] = process.argv.slice(2);
const versions = fs.existsSync(versionsPath)
  ? JSON.parse(fs.readFileSync(versionsPath, 'utf8'))
  : [];
const updated = [latestStable, ...versions.filter((version) => version !== latestStable)];

fs.writeFileSync(versionsPath, `${JSON.stringify(updated, null, 2)}\n`);
NODE

echo "Released documentation version: $latest_stable"
