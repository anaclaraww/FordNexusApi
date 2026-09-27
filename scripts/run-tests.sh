#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
rm -rf TestResults
mkdir -p TestResults
dotnet test FordNexus.sln \
  --logger "console;verbosity=normal" \
  --logger "trx;LogFileName=test-results.trx" \
  --logger "html;LogFileName=test-results.html" \
  --results-directory ./TestResults \
  --collect:"XPlat Code Coverage" 2>&1 | tee TestResults/test-output.log
echo
echo "Evidências geradas em ./TestResults"
