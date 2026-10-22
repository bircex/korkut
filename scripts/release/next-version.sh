#!/usr/bin/env bash
# Prints the next CalVer release version, YYYY.MM.PATCH: this month's first release is
# YYYY.MM.0, each further release in the same month adds one to PATCH. Months are not zero-padded
# (2026.7.0, 2026.10.3), matching the existing tags. Reads the v* tags of the repository.
#
#   scripts/release/next-version.sh            # e.g. 2026.10.0
#   scripts/release/next-version.sh 2026-11-02 # as if released on that date (for tests)
set -euo pipefail

date="${1:-$(date -u +%Y-%m-%d)}"
year="${date%%-*}"
month="$(echo "$date" | cut -d- -f2 | sed 's/^0//')"
prefix="$year.$month"

last="$(git tag --list "v$prefix.*" | sed "s/^v$prefix\.//" | grep -E '^[0-9]+$' | sort -n | tail -1 || true)"
if [ -z "$last" ]; then
  echo "$prefix.0"
else
  echo "$prefix.$((last + 1))"
fi
