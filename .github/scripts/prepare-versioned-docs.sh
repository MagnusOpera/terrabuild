#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$script_dir/../.." && pwd)"
website_dir="$repo_root/website"

# Stable promotion is explicit and independent of application/website releases.
tag="$(node -p 'require(process.argv[1]).tag' "$website_dir/stable.json")"
if [[ ! "$tag" =~ ^v?[0-9]+\.[0-9]+\.[0-9]+$ ]] ||
   ! git -C "$repo_root" cat-file -e "refs/tags/$tag:website" 2>/dev/null; then
  echo "Stable documentation requires an existing stable application tag: $tag" >&2
  exit 1
fi
if [[ ! -d "$website_dir/node_modules" ]]; then
  echo "website/node_modules is required before preparing Stable documentation." >&2
  exit 1
fi

temp_dir="$(mktemp -d "${TMPDIR:-/tmp}/terrabuild-doc-version.XXXXXX")"
trap 'rm -rf "$temp_dir"' EXIT

git -C "$repo_root" archive "refs/tags/$tag" | tar -x -C "$temp_dir"
ln -s "$website_dir/node_modules" "$temp_dir/website/node_modules"
"$script_dir/generate-whats-new.sh" "$tag" "$temp_dir/CHANGELOG.md" \
  "$temp_dir/website/site-docs/whats-new.md"
case "${tag#v}" in
  0.208.0)
    # This snapshot predates the .NET 11 nullability fixes.
    NoWarn="${NoWarn:+$NoWarn;}3261;3262" make -C "$temp_dir" docs
    ;;
  *) make -C "$temp_dir" docs ;;
esac
(
  cd "$temp_dir/website"
  ./node_modules/.bin/docusaurus docs:version stable
)

node - "$website_dir" "$temp_dir/website" <<'NODE'
const fs = require('node:fs');
const path = require('node:path');
const [websiteDir, source] = process.argv.slice(2);
// Only Stable is snapshotted; the working tree supplies Next.
for (const directory of ['versioned_docs', 'versioned_sidebars']) {
  fs.rmSync(path.join(websiteDir, directory), {recursive: true, force: true});
  fs.mkdirSync(path.join(websiteDir, directory), {recursive: true});
}
fs.cpSync(path.join(source, 'versioned_docs/version-stable'),
  path.join(websiteDir, 'versioned_docs/version-stable'), {recursive: true});
fs.copyFileSync(path.join(source, 'versioned_sidebars/version-stable-sidebars.json'),
  path.join(websiteDir, 'versioned_sidebars/version-stable-sidebars.json'));
fs.writeFileSync(path.join(websiteDir, 'versions.json'), '["stable"]\n');
NODE

echo "Stable documentation generated from $tag."
