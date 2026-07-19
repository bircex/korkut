#!/usr/bin/env bash
# KDM-004 — portable secret scanner used as a required migration-CI gate.
#
# Fails (exit 1) if any tracked file under the given paths contains a value that
# looks like a live secret: provider API keys, private keys, bearer/JWT creds, or
# a populated *_KEY/*_SECRET/PASSWORD/TOKEN assignment. It is intentionally
# dependency-free (pure grep) so it runs identically in CI and locally, and on
# fork PRs where secrets are unavailable.
#
# It scans file CONTENT only and never prints the matched secret value (prints the
# file + rule that matched). ``.env`` / example files and this scanner's own
# fixtures are excluded via .gitignore-tracked scoping: by default it scans only
# git-tracked files, so an operator's local uncommitted .env is never read.
#
# Usage:  secret-scan.sh [path ...]     (default: current dir, git-tracked files)
set -uo pipefail

ROOTS=("$@")
[ "${#ROOTS[@]}" -eq 0 ] && ROOTS=(".")

# Collect git-tracked files under the roots (falls back to find if not a repo).
list_files() {
  if git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    git ls-files -- "${ROOTS[@]}"
  else
    find "${ROOTS[@]}" -type f
  fi
}

# rule|regex — value-shaped secret patterns. Kept narrow to avoid false positives
# on documentation that merely names a variable.
RULES=(
  'anthropic-key|sk-ant-[A-Za-z0-9_-]{16,}'
  'openai-key|sk-[A-Za-z0-9]{20,}'
  'google-key|AIza[A-Za-z0-9_-]{20,}'
  'aws-key|AKIA[0-9A-Z]{16}'
  'private-key|-----BEGIN [A-Z ]*PRIVATE KEY-----'
  'jwt|eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{6,}'
  'bearer|[Bb]earer[[:space:]]+[A-Za-z0-9._-]{16,}'
  'assigned-secret|(API_KEY|APIKEY|SECRET|PASSWORD|TOKEN)[[:space:]]*[:=][[:space:]]*["'"'"']?[A-Za-z0-9/+_-]{16,}'
)

# Files that legitimately contain secret-shaped patterns for their own purpose
# (this scanner's regexes, the scanner's tests, and env TEMPLATES with no values).
EXCLUDE_RE='(ci/lib/secret-scan\.sh|ci/test/|\.env\.example|env/\.env\.example)'

found=0
while IFS= read -r f; do
  [ -f "$f" ] || continue
  printf '%s\n' "$f" | grep -Eq "$EXCLUDE_RE" && continue
  # Skip binary files.
  grep -Iq . "$f" 2>/dev/null || continue
  for rule in "${RULES[@]}"; do
    name="${rule%%|*}"; re="${rule#*|}"
    if grep -Eqn "$re" "$f" 2>/dev/null; then
      line=$(grep -En "$re" "$f" | head -1 | cut -d: -f1)
      echo "SECRET[$name]: $f:$line (value redacted)"
      found=1
    fi
  done
done < <(list_files)

if [ "$found" -ne 0 ]; then
  echo "secret-scan: FAIL — potential secret(s) found; remove them and use env/secret storage." >&2
  exit 1
fi
echo "secret-scan: OK — no secret-shaped values in tracked files."
