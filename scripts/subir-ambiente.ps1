$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

function New-RandomKey([int]$bytes) {
    $buffer = New-Object byte[] $bytes
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($buffer)
    return [Convert]::ToBase64String($buffer)
}

if (-not (Test-Path .env)) {
    Write-Host "Gerando .env com chaves aleatórias (fica só na sua máquina, fora do Git)..."
    @(
        "JWT_SIGNING_KEY=$(New-RandomKey 64)"
        "ENCRYPTION_KEY=$(New-RandomKey 32)"
        "GRAFANA_ADMIN_PASSWORD=$(New-RandomKey 18)"
    ) | Set-Content -Path .env -Encoding ascii
}

Write-Host "Gerando certificados MQTT (CA, broker, API e veículos)..."
docker compose --profile setup run --rm mqtt-certs
if ($LASTEXITCODE -ne 0) { throw "Falha ao gerar certificados" }

Write-Host "Subindo API, broker MQTT, Prometheus, Loki e Grafana..."
docker compose up -d --build
if ($LASTEXITCODE -ne 0) { throw "Falha ao subir o ambiente" }

$grafanaPassword = (Get-Content .env | Where-Object { $_ -like "GRAFANA_ADMIN_PASSWORD=*" }) -replace "GRAFANA_ADMIN_PASSWORD=", ""
Write-Host ""
Write-Host "API ........ http://localhost:8080/health"
Write-Host "Prometheus . http://localhost:9090/alerts"
Write-Host "Grafana .... http://localhost:3000  (usuário admin, senha $grafanaPassword)"
Write-Host ""
Write-Host "Próximos passos: .\scripts\gerar-trafego.ps1 e .\scripts\simular-telemetria.ps1"
