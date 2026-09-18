#!/bin/sh
set -eu
test "$1" = "argument with spaces"
test "$TB_APPLE_SAMPLE" = "/terrabuild-home/cache with spaces"
test "$TB_APPLE_EXPLICIT" = "explicit value"
test "$HOME" = "/terrabuild-home"
test "$TERRABUILD_HOME" = "$HOME"
test "$TMPDIR" = "/terrabuild-tmp"
test "$(uname -m)" = "aarch64"
test "$(pwd)" = "/terrabuild/$(basename "$PWD")"
home_probe=$(mktemp "$HOME/apple-smoke.XXXXXX")
tmp_probe=$(mktemp "$TMPDIR/apple-smoke.XXXXXX")
rm "$home_probe" "$tmp_probe"
mkdir -p .out
printf 'apple container output\n' > '.out/result with spaces.txt'
printf 'apple stdout\n'
printf 'apple stderr\n' >&2
