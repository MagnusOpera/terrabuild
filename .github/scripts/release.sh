#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 || $# -gt 2 ]]; then
  echo "Usage: $0 <version> [dryrun]"
  echo "  version: X.Y.Z or X.Y.Z-next"
  echo "  dryrun : true|false (default: false)"
  exit 2
fi

version="$1"
dryrun="${2:-false}"

if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-next)?$ ]]; then
  echo "ERROR: Invalid version '$version'. Expected X.Y.Z or X.Y.Z-next."
  exit 1
fi

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

if [[ ! -f CHANGELOG.md ]]; then
  echo "ERROR: CHANGELOG.md not found."
  exit 1
fi

if [[ "$dryrun" != "true" && -n "$(git status --porcelain)" ]]; then
  echo "ERROR: Working tree is not clean. Commit or stash changes before running release."
  exit 1
fi

if git rev-parse -q --verify "refs/tags/${version}" >/dev/null; then
  echo "ERROR: Tag '${version}' already exists."
  exit 1
fi

if grep -q "^## \[${version}\]$" CHANGELOG.md; then
  echo "ERROR: CHANGELOG section '## [${version}]' already exists."
  exit 1
fi

section_header="## [Unreleased]"

unreleased_body="$(
  awk -v header="$section_header" '
    BEGIN { in_section = 0 }
    $0 == header { in_section = 1; next }
    /^## \[/ && in_section { exit }
    in_section { print }
  ' CHANGELOG.md
)"

if [[ -z "${unreleased_body//[[:space:]]/}" ]]; then
  echo "ERROR: Unreleased section is empty."
  exit 1
fi

if ! grep -q '^[[:space:]]*-\s\+' <<<"$unreleased_body"; then
  echo "ERROR: Unreleased section must include at least one bullet."
  exit 1
fi

if [[ "$version" == *-next ]]; then
  next_tags="$(git tag --list '*-next' | sort -Vu)"
  previous_tag="$(
    {
      printf "%s\n" "$next_tags"
      printf "%s\n" "$version"
    } \
    | sort -Vu \
    | awk -v target="$version" '
        $0 == target { print prev; exit }
        { prev = $0 }
      '
  )"
else
  stable_tags="$(
    git tag --list \
    | grep -E '^[0-9]+\.[0-9]+\.[0-9]+$' \
    | sort -Vu || true
  )"
  previous_tag="$(
    {
      printf "%s\n" "$stable_tags"
      printf "%s\n" "$version"
    } \
    | sort -Vu \
    | awk -v target="$version" '
        $0 == target { print prev; exit }
        { prev = $0 }
      '
  )"
fi

if [[ -z "$previous_tag" ]]; then
  echo "ERROR: Could not determine previous tag for '$version'."
  echo "Create the compare link manually in CHANGELOG.md and tag manually."
  exit 1
fi

compare_link="**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/${previous_tag}...${version}"

tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT

stripped_changelog="${tmp_dir}/changelog-stripped.md"
new_section_file="${tmp_dir}/new-section.md"
updated_changelog="${tmp_dir}/CHANGELOG.md"

awk '
  BEGIN { skip = 0 }
  $0 == "## [Unreleased]" {
    print
    print ""
    skip = 1
    next
  }
  skip && /^## \[/ { skip = 0 }
  !skip { print }
' CHANGELOG.md > "$stripped_changelog"

{
  echo "## [${version}]"
  echo ""
  printf "%s\n" "$unreleased_body"
  echo ""
  echo "$compare_link"
} > "$new_section_file"

awk -v section_file="$new_section_file" '
  BEGIN { inserted = 0; skip_next_blank = 0 }
  $0 == "## [Unreleased]" && inserted == 0 {
    print
    print ""
    while ((getline line < section_file) > 0) {
      print line
    }
    close(section_file)
    print ""
    inserted = 1
    skip_next_blank = 1
    next
  }
  skip_next_blank == 1 && $0 == "" {
    skip_next_blank = 0
    next
  }
  { print }
' "$stripped_changelog" > "$updated_changelog"

if ! grep -q "^## \[${version}\]$" "$updated_changelog"; then
  echo "ERROR: Failed to materialize CHANGELOG section for ${version}."
  exit 1
fi

if [[ "$dryrun" == "true" ]]; then
  echo "[DRY RUN] Would update CHANGELOG.md, commit and create annotated tag '${version}'."
  echo "[DRY RUN] Previous tag: ${previous_tag}"
  echo "[DRY RUN] Compare link: ${compare_link}"
  exit 0
fi

cp "$updated_changelog" CHANGELOG.md

git add CHANGELOG.md
git commit -m "chore(release): ${version}"
git tag -a "${version}" -m "Release ${version}"

echo "Release prepared successfully."
echo "Next steps:"
echo "  git push origin main --follow-tags"
