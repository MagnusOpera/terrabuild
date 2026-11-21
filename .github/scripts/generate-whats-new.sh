#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 3 || $# -gt 4 ]]; then
  echo "Usage: $0 <version|latest-next|since:stable-version> <changelog> <output> [compare-endpoint]" >&2
  exit 2
fi

selector="$1"
changelog="$2"
output="$3"
compare_endpoint="${4:-}"

if [[ ! -f "$changelog" ]]; then
  echo "ERROR: Changelog '$changelog' not found." >&2
  exit 1
fi

versions="$({
  sed -nE 's/^## \[([0-9]+\.[0-9]+\.[0-9]+(-next)?)\]$/\1/p' "$changelog"
} | sort -Vu)"

if [[ "$selector" == since:* ]]; then
  baseline="${selector#since:}"
  baseline_version="${baseline#v}"
  if [[ ! "$baseline_version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] ||
     ! grep -qxF "$baseline_version" <<<"$versions"; then
    echo "ERROR: Stable baseline '$baseline' not found in '$changelog'." >&2
    exit 1
  fi
  heading="Next"
  include_unreleased=true
  compare_endpoint="${compare_endpoint:-$(git rev-parse HEAD)}"
  siblings=()
  # Changelog order defines changes since Stable, including preview releases.
  while IFS= read -r candidate; do
    [[ -n "$candidate" ]] && siblings+=("$candidate")
  done < <(awk -v baseline="## [$baseline_version]" '
    $0 == baseline { exit }
    /^## \[[0-9]+\.[0-9]+\.[0-9]+(-next)?\]$/ {
      sub(/^## \[/, ""); sub(/\]$/, ""); print
    }
  ' "$changelog" | awk '{ lines[NR] = $0 } END { for (i = NR; i > 0; i--) print lines[i] }')
elif [[ "$selector" == "latest-next" ]]; then
  target="$(grep -- '-next$' <<<"$versions" | tail -1 || true)"
  if [[ -z "$target" ]]; then
    echo "ERROR: No preview release section found in '$changelog'." >&2
    exit 1
  fi
  heading="Next"
  include_unreleased=true
  if [[ -z "$compare_endpoint" ]]; then
    compare_endpoint="$(git rev-parse HEAD)"
  fi
elif [[ "$selector" =~ ^v?[0-9]+\.[0-9]+\.[0-9]+(-next)?$ ]]; then
  target="${selector#v}"
  heading="$target"
  include_unreleased=false
  compare_endpoint="${compare_endpoint:-$selector}"
else
  echo "ERROR: Invalid selector '$selector'. Expected X.Y.Z, X.Y.Z-next, latest-next, or since:X.Y.Z." >&2
  exit 1
fi

extract_bullets() {
  local section="$1"
  awk -v header="## [$section]" '
    BEGIN { in_section = 0; in_bullet = 0 }
    $0 == header { in_section = 1; next }
    in_section && /^## \[/ { exit }
    in_section && /^[[:space:]]*-[[:space:]]+/ { print; in_bullet = 1; next }
    in_section && in_bullet && /^[[:space:]]{2,}[^[:space:]]/ { print; next }
    in_section { in_bullet = 0 }
  ' "$changelog"
}

if [[ "$selector" != since:* ]]; then
  if ! grep -qxF "$target" <<<"$versions"; then
    echo "ERROR: Release section '## [$target]' not found in '$changelog'." >&2
    exit 1
  fi

  if [[ ! "$target" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)(-next)?$ ]]; then
    echo "ERROR: Could not parse target version '$target'." >&2
    exit 1
  fi

  family_major="${BASH_REMATCH[1]}"
  family_minor="${BASH_REMATCH[2]}"
  target_revision="${BASH_REMATCH[3]}"
  channel="${BASH_REMATCH[4]:-}"

  siblings=()
  while IFS= read -r candidate; do
    [[ -z "$candidate" ]] && continue
    if [[ "$candidate" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)(-next)?$ ]] \
      && [[ "${BASH_REMATCH[1]}" == "$family_major" ]] \
      && [[ "${BASH_REMATCH[2]}" == "$family_minor" ]] \
      && [[ "${BASH_REMATCH[4]:-}" == "$channel" ]] \
      && (( 10#${BASH_REMATCH[3]} <= 10#$target_revision )); then
      siblings+=("$candidate")
    fi
  done <<<"$versions"

  if (( ${#siblings[@]} == 0 )); then
    echo "ERROR: No sibling releases found for '$target'." >&2
    exit 1
  fi

  earliest="${siblings[0]}"
  earliest_body="$(
    awk -v header="## [$earliest]" '
      BEGIN { in_section = 0 }
      $0 == header { in_section = 1; next }
      in_section && /^## \[/ { exit }
      in_section { print }
    ' "$changelog"
  )"
  compare_line="$(grep -m1 '^\*\*Full Changelog\*\*: https://github.com/magnusopera/terrabuild/compare/' <<<"$earliest_body" || true)"

  if [[ -z "$compare_line" ]]; then
    echo "ERROR: Earliest sibling '$earliest' has no valid Full Changelog link." >&2
    exit 1
  fi

  compare_range="${compare_line##*/compare/}"
  baseline="${compare_range%%...*}"
  earliest_endpoint="${compare_range#*...}"
  if [[ -z "$baseline" || "${earliest_endpoint#v}" != "$earliest" ]]; then
    echo "ERROR: Full Changelog link for '$earliest' does not end at that release." >&2
    exit 1
  fi

fi # Exact releases and legacy preview-family selection.

output_dir="$(dirname "$output")"
if [[ ! -d "$output_dir" ]]; then
  echo "ERROR: Output directory '$output_dir' not found." >&2
  exit 1
fi

tmp_output="$(mktemp "${output}.tmp.XXXXXX")"
trap 'rm -f "$tmp_output"' EXIT

{
  echo "---"
  echo "id: whats-new"
  echo "title: What's New"
  echo "slug: /whats-new"
  echo "---"
  echo ""
  echo "For the complete history, see the full [CHANGELOG.md](https://github.com/MagnusOpera/Terrabuild/blob/main/CHANGELOG.md) on GitHub."
  echo ""
  echo "## ${heading}"
  echo ""

  if [[ "$include_unreleased" == "true" ]]; then
    unreleased_body="$(extract_bullets "Unreleased")"
    if [[ -n "${unreleased_body//[[:space:]]/}" ]]; then
      echo "### Unreleased"
      echo ""
      printf '%s\n' "$unreleased_body"
      echo ""
    fi
  fi

  for (( index=${#siblings[@]}-1; index >= 0; index-- )); do
    sibling="${siblings[index]}"
    body="$(extract_bullets "$sibling")"
    if [[ -z "${body//[[:space:]]/}" ]]; then
      echo "ERROR: Release section '## [$sibling]' has no bullet entries." >&2
      exit 1
    fi
    echo "### ${sibling}"
    echo ""
    printf '%s\n' "$body"
    echo ""
  done

  echo "**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/${baseline}...${compare_endpoint}"
} > "$tmp_output"

chmod 644 "$tmp_output"
mv "$tmp_output" "$output"
trap - EXIT
