#!/usr/bin/env bash
set -euo pipefail

check=false
if [[ "${1:-}" == "--check" ]]; then
  check=true
  shift
fi
if [[ $# -gt 1 ]]; then
  echo "Usage: $0 [--check] [CHANGELOG.md]" >&2
  exit 2
fi
changelog="${1:-CHANGELOG.md}"
if [[ ! -f "$changelog" ]]; then
  echo "ERROR: Changelog not found: $changelog" >&2
  exit 1
fi
formatted="$(mktemp)"
trap 'rm -f "$formatted"' EXIT

# Keep Markdown section spacing, but do not turn adjacent one-line entries into
# separate loose lists. Buffer empty lines to trim the start/end of the file too.
awk '
  /^[[:space:]]*$/ { blank = 1; next }
  {
    heading = /^#+[[:space:]]/
    compare = /^\*\*Full Changelog\*\*:/
    if (seen && (heading || previous_heading || compare || previous_compare || (blank && !(previous_bullet && /^[-*+] /)))) print ""
    print
    previous_bullet = /^[-*+] /
    previous_heading = heading
    previous_compare = compare
    blank = 0
    seen = 1
  }
' "$changelog" > "$formatted"

if ! cmp -s "$changelog" "$formatted"; then
  if [[ "$check" == "true" ]]; then
    echo "ERROR: Changelog spacing is inconsistent. Run make format-changelog." >&2
    diff -u "$changelog" "$formatted" || true
    exit 1
  fi
  cat "$formatted" > "$changelog"
fi
