#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 <version>"
  exit 2
fi

version="$1"
section_header="## [${version}]"

if [[ ! -f CHANGELOG.md ]]; then
  echo "ERROR: CHANGELOG.md not found."
  exit 1
fi

section_body="$(
  awk -v header="$section_header" '
    BEGIN { in_section = 0 }
    $0 == header { in_section = 1; next }
    /^## \[/ && in_section { exit }
    in_section { print }
  ' CHANGELOG.md
)"

if [[ -z "${section_body//[[:space:]]/}" ]]; then
  echo "ERROR: Missing or empty changelog section '${section_header}'."
  echo "Add a '${section_header}' section to CHANGELOG.md before tagging."
  exit 1
fi

if ! grep -q '^[[:space:]]*-\s\+' <<<"$section_body"; then
  echo "ERROR: Section '${section_header}' must include at least one bullet line."
  exit 1
fi

if ! grep -q '\*\*Full Changelog\*\*:' <<<"$section_body"; then
  echo "ERROR: Section '${section_header}' must include a '**Full Changelog**' compare link."
  exit 1
fi

printf '%s\n' "$section_body"
