#!/usr/bin/env bash
# Writes this tree's build warnings and test totals to $1 in a form two runs can diff.
# Warning locations lose their line and column: rewriting comments moves lines, not code.
set -uo pipefail
out="$1"
root="$(git rev-parse --show-toplevel)"
build="$(mktemp)"
tests="$(mktemp)"
cd "$root" || exit 1

if ! dotnet build DungeonMasterXIV.sln --no-incremental -nologo > "$build" 2>&1; then
  tail -40 "$build"; echo "BUILD FAILED"; exit 1
fi
if ! dotnet test DungeonMasterXIV.sln --no-build -nologo > "$tests" 2>&1; then
  tail -60 "$tests"; echo "TESTS FAILED"; exit 1
fi

{
  echo "== warnings"
  grep -E ': (warning|error) [A-Z]+[0-9]+' "$build" \
    | sed -E "s#${root}/##; s/\([0-9]+,[0-9]+\)//; s/ \[[^]]*\]$//" | sort -u
  echo "== tests"
  grep -E '^(Passed|Failed)!' "$tests" | sed -E 's/Duration: [^-]*- //' | sort
} > "$out"
cat "$out"
