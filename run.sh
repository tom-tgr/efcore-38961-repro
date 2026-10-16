#!/usr/bin/env bash
# Runs the repro suite once per EF Core version (default: last EF9 vs current EF10).
#   ./run.sh                     -> 9.0.21 and 10.0.13
#   ./run.sh 10.0.0 11.0.0-...   -> any versions you like
# Set EF38961_SQLSERVER to also run the SQL Server timing test.
set -u
cd "$(dirname "$0")"

for v in "${@:-9.0.21 10.0.13}"; do
  for ver in $v; do
    echo
    echo "==================== EF Core $ver ===================="
    dotnet test -p:EfVer="$ver" --artifacts-path "artifacts/$ver" \
      --logger "console;verbosity=normal" 2>&1 \
      | grep -E "Passed |Failed |Skipped |scenario=|Assert\.|Total tests|Passed!|Failed!"
  done
done
