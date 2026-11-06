#!/usr/bin/env bash
set -euo pipefail

scripts="$(cd "$(dirname "$0")" && pwd)"
fixture="$(mktemp -d)"
trap 'rm -rf "$fixture"' EXIT
mkdir -p "$fixture/repo/.github/scripts" "$fixture/repo/website/site-docs" "$fixture/bin"
cp "$scripts/release.sh" "$scripts/format-changelog.sh" "$fixture/repo/.github/scripts/"

first_version=0.2.0
second_version=0.3.0
slug=example/changelog
first_baseline=0.1.0
if [[ -f "$scripts/generate-whats-new.sh" ]]; then
  cp "$scripts/generate-whats-new.sh" "$fixture/repo/.github/scripts/"
  first_version=0.2.0-next
  second_version=0.2.0
  slug=magnusopera/terrabuild
  first_baseline=0.1.0-next
fi

# Website tooling is unrelated to changelog generation. No package installs,
# builds, network requests, real-repository commits, or pushes are performed.
cat > "$fixture/bin/npm" <<'STUB'
#!/usr/bin/env bash
exit 0
STUB
chmod +x "$fixture/bin/npm"
export PATH="$fixture/bin:$PATH"

cat > "$fixture/repo/CHANGELOG.md" <<'CHANGELOG'
# Changelog


Introductory text.

## [Unreleased]


- First change.

- Second change.


## [0.1.0]


- Existing change.


CHANGELOG
cat > "$fixture/expected.md" <<'CHANGELOG'
# Changelog

Introductory text.

## [Unreleased]

- First change.
- Second change.

## [0.1.0]

- Existing change.
CHANGELOG
if "$scripts/format-changelog.sh" --check "$fixture/repo/CHANGELOG.md" >/dev/null 2>&1; then
  echo "ERROR: Formatting check accepted extra blank lines." >&2
  exit 1
fi
cp "$fixture/repo/CHANGELOG.md" "$fixture/input.md"
"$scripts/format-changelog.sh" "$fixture/input.md"
diff -u "$fixture/expected.md" "$fixture/input.md"
"$scripts/format-changelog.sh" --check "$fixture/input.md"

printf '# Changelog\n## [Unreleased]\n- Change.\n**Full Changelog**: example\n' > "$fixture/compact.md"
printf '# Changelog\n\n## [Unreleased]\n\n- Change.\n\n**Full Changelog**: example\n' > "$fixture/compact-expected.md"
"$scripts/format-changelog.sh" "$fixture/compact.md"
diff -u "$fixture/compact-expected.md" "$fixture/compact.md"

printf '* First item.\n\n* Second item.\n' > "$fixture/stars.md"
printf '* First item.\n* Second item.\n' > "$fixture/stars-expected.md"
"$scripts/format-changelog.sh" "$fixture/stars.md"
diff -u "$fixture/stars-expected.md" "$fixture/stars.md"

cd "$fixture/repo"
printf 'fixture\n' > website/site-docs/whats-new.md
git init -q
git config user.name 'Changelog test'
git config user.email 'changelog@example.invalid'
git config commit.gpgSign false
git config tag.gpgSign false
git config core.hooksPath /dev/null
git remote add origin git@github.com:example/changelog.git
git add .
git commit -qm fixture
git tag 0.1.0
git tag 0.1.0-next

# A dry run must leave both content and Git state untouched.
head_before="$(git rev-parse HEAD)"
.github/scripts/release.sh "$first_version" true > "$fixture/dryrun.log" 2>&1 || { cat "$fixture/dryrun.log"; exit 1; }
[[ "$(git rev-parse HEAD)" == "$head_before" ]]
git diff --exit-code
if git rev-parse --verify "refs/tags/$first_version" >/dev/null 2>&1; then
  echo "ERROR: Dry run created a release tag." >&2
  exit 1
fi

.github/scripts/release.sh "$first_version" > "$fixture/first-release.log" 2>&1 || { cat "$fixture/first-release.log"; exit 1; }
"$scripts/format-changelog.sh" --check CHANGELOG.md
cat > "$fixture/expected-release.md" <<CHANGELOG
# Changelog

Introductory text.

## [Unreleased]

## [$first_version]

- First change.
- Second change.

**Full Changelog**: https://github.com/$slug/compare/$first_baseline...$first_version

## [0.1.0]

- Existing change.
CHANGELOG
diff -u "$fixture/expected-release.md" CHANGELOG.md

# A second release must not accumulate blanks around headings or bullet lists.
awk '{ print; if ($0 == "## [Unreleased]") print "\n\n- Third change.\n\n" }' CHANGELOG.md > "$fixture/next.md"
cat "$fixture/next.md" > CHANGELOG.md
git add CHANGELOG.md
git commit -qm 'Add another change'
.github/scripts/release.sh "$second_version" > "$fixture/second-release.log" 2>&1 || { cat "$fixture/second-release.log"; exit 1; }
"$scripts/format-changelog.sh" --check CHANGELOG.md
grep -q '^- First change\.$' CHANGELOG.md
grep -q '^- Second change\.$' CHANGELOG.md
grep -q '^- Third change\.$' CHANGELOG.md
git diff --exit-code
printf 'Changelog formatting and consecutive-release tests passed.\n'
