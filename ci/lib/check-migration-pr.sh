#!/usr/bin/env bash
# KDM-001 — Migration branch/PR governance policy engine.
#
# Enforces the .NET migration branch/merge contract on a single pull request's
# metadata. It is intentionally pure: it reads the PR's base (target) and head
# (source) branch names from arguments or environment and decides accept/reject.
# No network, no GitHub API — so it is deterministic and unit-testable both in CI
# and locally (see scripts/test-migration-pr-policy.sh).
#
# Contract (from IMPORTANT/README_DOTNET_MIGRATION_TASKS.md and KDM-001):
#   * Implementation branches are named `migration/kdm-NNN-<short-topic>`.
#   * They may ONLY target the `dotnet-migration` branch.
#   * No migration PR may target `main` or `master`.
#   * Any PR into `dotnet-migration` MUST come from a conforming `migration/kdm-*`
#     branch (keeps ad-hoc branches out of the migration line).
#   * The final `dotnet-migration` -> default-branch promotion is governed
#     separately and is out of scope for this check (neither accepted nor
#     rejected here — it simply is not a migration-implementation PR).
#
# Usage:
#   check-migration-pr.sh --base <target-branch> --head <source-branch>
#   BASE_REF=<target> HEAD_REF=<source> check-migration-pr.sh
#
# Exit codes:
#   0  PR metadata complies with the migration policy (or is out of scope).
#   1  PR metadata violates the migration policy.
#   2  Usage / input error.
set -euo pipefail

BASE_REF="${BASE_REF:-}"
HEAD_REF="${HEAD_REF:-}"

while [ "$#" -gt 0 ]; do
  case "$1" in
    --base) BASE_REF="${2:-}"; shift 2 ;;
    --head) HEAD_REF="${2:-}"; shift 2 ;;
    -h|--help)
      grep '^#' "$0" | sed 's/^# \{0,1\}//'
      exit 0 ;;
    *) echo "check-migration-pr: unknown argument '$1'" >&2; exit 2 ;;
  esac
done

if [ -z "$BASE_REF" ] || [ -z "$HEAD_REF" ]; then
  echo "check-migration-pr: BASE_REF and HEAD_REF are required" >&2
  exit 2
fi

# Normalise refs like `refs/heads/x` or `owner:x` down to the bare branch name.
strip_ref() {
  local r="$1"
  r="${r#refs/heads/}"
  r="${r##*:}"
  printf '%s' "$r"
}
BASE_REF="$(strip_ref "$BASE_REF")"
HEAD_REF="$(strip_ref "$HEAD_REF")"

MIGRATION_BRANCH="dotnet-migration"
# migration/kdm-NNN-<topic>: 3-digit id, lowercase kebab topic.
IMPL_BRANCH_RE='^migration/kdm-[0-9]{3}-[a-z0-9]+(-[a-z0-9]+)*$'

is_default_branch() { [ "$1" = "main" ] || [ "$1" = "master" ]; }
is_impl_branch()    { printf '%s' "$1" | grep -Eq "$IMPL_BRANCH_RE"; }

fail() { echo "REJECT: $1" >&2; exit 1; }
pass() { echo "ACCEPT: $1"; exit 0; }

# RULE 1 — a migration implementation branch must never target a default branch.
if is_impl_branch "$HEAD_REF" && is_default_branch "$BASE_REF"; then
  fail "migration branch '$HEAD_REF' targets '$BASE_REF'; migration PRs may only target '$MIGRATION_BRANCH'."
fi

# RULE 2 — a migration implementation branch may target ONLY dotnet-migration.
if is_impl_branch "$HEAD_REF" && [ "$BASE_REF" != "$MIGRATION_BRANCH" ]; then
  fail "migration branch '$HEAD_REF' targets '$BASE_REF'; it must target '$MIGRATION_BRANCH'."
fi

# RULE 3 — anything merging INTO dotnet-migration must be a conforming impl branch.
if [ "$BASE_REF" = "$MIGRATION_BRANCH" ] && ! is_impl_branch "$HEAD_REF"; then
  fail "head '$HEAD_REF' targets '$MIGRATION_BRANCH' but is not a 'migration/kdm-NNN-<topic>' branch."
fi

# RULE 4 — the sanctioned dotnet-migration -> default promotion is out of scope.
if [ "$HEAD_REF" = "$MIGRATION_BRANCH" ] && is_default_branch "$BASE_REF"; then
  pass "'$HEAD_REF' -> '$BASE_REF' is the release-promotion path; governed by separate approval, not this check."
fi

# migration impl branch -> dotnet-migration: the happy path.
if is_impl_branch "$HEAD_REF" && [ "$BASE_REF" = "$MIGRATION_BRANCH" ]; then
  pass "'$HEAD_REF' -> '$BASE_REF' complies with the migration branch/PR contract."
fi

pass "'$HEAD_REF' -> '$BASE_REF' is outside migration governance scope; no migration rule applies."
