#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
generator="${repo_root}/.github/scripts/generate-whats-new.sh"
tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT

changelog="${tmp_dir}/CHANGELOG.md"
cat > "$changelog" <<'EOF'
# Changelog

## [Unreleased]

- unreleased

## [0.2.10-next]

- preview ten

**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/0.2.9-next...0.2.10-next

## [0.2.10]

- stable ten

**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/0.2.9...0.2.10

## [0.2.9]

- stable nine

**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/0.1.4...0.2.9

## [0.2.9-next]

- preview nine

**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/0.1.4-next...0.2.9-next

## [0.1.4]

- old stable

**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/0.1.3...0.1.4

## [0.1.4-next]

- old preview

**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/0.1.3-next...0.1.4-next
EOF

next_output="${tmp_dir}/next.md"
"$generator" latest-next "$changelog" "$next_output" website-commit

cat > "${tmp_dir}/expected-next.md" <<'EOF'
---
id: whats-new
title: What's New
slug: /whats-new
---

For the complete history, see the full [CHANGELOG.md](https://github.com/MagnusOpera/Terrabuild/blob/main/CHANGELOG.md) on GitHub.

## Next

### Unreleased

- unreleased

### 0.2.10-next

- preview ten

### 0.2.9-next

- preview nine

**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/0.1.4-next...website-commit
EOF

diff -u "${tmp_dir}/expected-next.md" "$next_output"

stable_output="${tmp_dir}/stable.md"
"$generator" 0.2.10 "$changelog" "$stable_output"

cat > "${tmp_dir}/expected-stable.md" <<'EOF'
---
id: whats-new
title: What's New
slug: /whats-new
---

For the complete history, see the full [CHANGELOG.md](https://github.com/MagnusOpera/Terrabuild/blob/main/CHANGELOG.md) on GitHub.

## 0.2.10

### 0.2.10

- stable ten

### 0.2.9

- stable nine

**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/0.1.4...0.2.10
EOF

diff -u "${tmp_dir}/expected-stable.md" "$stable_output"

preview_output="${tmp_dir}/preview.md"
"$generator" 0.2.9-next "$changelog" "$preview_output"
if grep -q -- 'preview ten\|unreleased\|stable nine' "$preview_output"; then
  echo "ERROR: Exact preview output included later, unreleased, or stable entries." >&2
  exit 1
fi
grep -qxF '### 0.2.9-next' "$preview_output"

preserved="${tmp_dir}/preserved.md"
printf '%s\n' preserved > "$preserved"
if "$generator" 0.3.0 "$changelog" "$preserved" >/dev/null 2>&1; then
  echo "ERROR: Missing release unexpectedly succeeded." >&2
  exit 1
fi
grep -qxF preserved "$preserved"

since_output="${tmp_dir}/since.md"
"$generator" since:0.2.9 "$changelog" "$since_output" website-commit
# Include every section above Stable, regardless of release channel.
grep -qxF '### Unreleased' "$since_output"
grep -qxF '### 0.2.10-next' "$since_output"
grep -qxF '### 0.2.10' "$since_output"
grep -qxF '**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/0.2.9...website-commit' "$since_output"
if grep -q -- 'stable nine\|preview nine\|old stable\|old preview' "$since_output"; then
  echo "ERROR: Next included changes at or before Stable." >&2
  exit 1
fi

# Next also works before the first preview tag exists.
cat > "${tmp_dir}/no-preview.md" <<'EOF'
## [Unreleased]

- new change

## [1.0.0]

- shipped change
EOF
"$generator" since:1.0.0 "${tmp_dir}/no-preview.md" "$since_output" website-commit
grep -qxF -- '- new change' "$since_output"
if grep -q -- 'shipped change' "$since_output"; then
  echo "ERROR: Next included the Stable release." >&2
  exit 1
fi
if "$generator" since:0.9.0 "${tmp_dir}/no-preview.md" "$preserved" >/dev/null 2>&1; then
  echo "ERROR: Missing Stable baseline unexpectedly succeeded." >&2
  exit 1
fi
grep -qxF preserved "$preserved"

# New tags have v prefixes, while headings and displayed versions stay numeric.
"$generator" v0.2.10 "$changelog" "$since_output"
grep -qxF '## 0.2.10' "$since_output"
grep -qxF '**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/0.1.4...v0.2.10' "$since_output"
"$generator" since:v0.2.9 "$changelog" "$since_output" website-commit
grep -qxF '**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/v0.2.9...website-commit' "$since_output"
# Subsequent release families can have prefixed compare endpoints too.
sed 's/0.1.4-next...0.2.9-next/v0.1.4-next...v0.2.9-next/' "$changelog" > "${tmp_dir}/prefixed.md"
"$generator" v0.2.9-next "${tmp_dir}/prefixed.md" "$since_output"
grep -qxF '**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/v0.1.4-next...v0.2.9-next' "$since_output"

echo "What's New generator tests passed."
