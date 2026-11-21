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
printf '/website/site-docs/whats-new.md\n' > .gitignore
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

# Reject bare tags, unsupported channels, and malformed semantic versions.
for invalid in 0.2.0 0.2.0-next v01.2.0 v0.2.0-beta website-0.2.0; do
  if .github/scripts/release.sh "$invalid" true > "$fixture/invalid.log" 2>&1; then
    echo "ERROR: Accepted invalid release tag: $invalid" >&2
    exit 1
  fi
  grep -q 'Expected vX.Y.Z or vX.Y.Z-next' "$fixture/invalid.log"
done
# Unrelated tags must not become preview baselines.
git tag website-99.0.0-next
git tag unrelated-next

# A dry run must leave both content and Git state untouched.
head_before="$(git rev-parse HEAD)"
.github/scripts/release.sh "v$first_version" true > "$fixture/dryrun.log" 2>&1 || { cat "$fixture/dryrun.log"; exit 1; }
[[ "$(git rev-parse HEAD)" == "$head_before" ]]
git diff --exit-code
if git rev-parse --verify "refs/tags/v$first_version" >/dev/null 2>&1; then
  echo "ERROR: Dry run created a release tag." >&2
  exit 1
fi

.github/scripts/release.sh "v$first_version" > "$fixture/first-release.log" 2>&1 || { cat "$fixture/first-release.log"; exit 1; }
"$scripts/format-changelog.sh" --check CHANGELOG.md
cat > "$fixture/expected-release.md" <<CHANGELOG
# Changelog

Introductory text.

## [Unreleased]

## [$first_version]

- First change.
- Second change.

**Full Changelog**: https://github.com/$slug/compare/$first_baseline...v$first_version

## [0.1.0]

- Existing change.
CHANGELOG
diff -u "$fixture/expected-release.md" CHANGELOG.md
[[ -s website/site-docs/whats-new.md ]]
[[ -z "$(git ls-files website/site-docs/whats-new.md)" ]]
[[ -z "$(git status --porcelain)" ]]

# A second release must not accumulate blanks around headings or bullet lists.
awk '{ print; if ($0 == "## [Unreleased]") print "\n\n- Third change.\n\n" }' CHANGELOG.md > "$fixture/next.md"
cat "$fixture/next.md" > CHANGELOG.md
git add CHANGELOG.md
git commit -qm 'Add another change'
.github/scripts/release.sh "v$second_version" > "$fixture/second-release.log" 2>&1 || { cat "$fixture/second-release.log"; exit 1; }
"$scripts/format-changelog.sh" --check CHANGELOG.md
grep -q '^- First change\.$' CHANGELOG.md
grep -q '^- Second change\.$' CHANGELOG.md
grep -q '^- Third change\.$' CHANGELOG.md
git diff --exit-code
# New releases use annotated v tags while changelog headings stay numeric.
[[ "$(git cat-file -t "refs/tags/v$first_version")" == tag ]]
[[ "$(git cat-file -t "refs/tags/v$second_version")" == tag ]]
if git show-ref --verify --quiet "refs/tags/$second_version"; then
  echo "ERROR: Created a bare release tag." >&2
  exit 1
fi
# A mixed history must resolve the actual prefixed previous tag by channel.
awk '{ print; if ($0 == "## [Unreleased]") print "\n- Fourth change." }' CHANGELOG.md > "$fixture/next.md"
cat "$fixture/next.md" > CHANGELOG.md
.github/scripts/release.sh v0.3.0-next true > "$fixture/preview-dryrun.log"
grep -qF "Previous tag: v$first_version" "$fixture/preview-dryrun.log"
.github/scripts/release.sh v0.3.0 true > "$fixture/stable-dryrun.log"
grep -qF "Previous tag: v$second_version" "$fixture/stable-dryrun.log"
grep -qF "compare/v$second_version...v0.3.0" "$fixture/stable-dryrun.log"
# Duplicate legacy tags are also rejected before changing anything.
git tag 0.4.0
if .github/scripts/release.sh v0.4.0 true > "$fixture/duplicate.log" 2>&1; then
  echo "ERROR: Accepted an existing legacy release." >&2
  exit 1
fi
grep -q 'already has a tag' "$fixture/duplicate.log"
printf 'Changelog formatting and consecutive-release tests passed.\n'
