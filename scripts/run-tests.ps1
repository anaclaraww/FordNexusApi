$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
if (Test-Path TestResults) { Remove-Item TestResults -Recurse -Force }
New-Item -ItemType Directory TestResults | Out-Null
dotnet test FordNexus.sln `
  --logger "console;verbosity=normal" `
  --logger "trx;LogFileName=test-results.trx" `
  --logger "html;LogFileName=test-results.html" `
  --results-directory ./TestResults `
  --collect:"XPlat Code Coverage" 2>&1 | Tee-Object -FilePath TestResults/test-output.log
Write-Host "`nEvidências geradas em .\TestResults"
