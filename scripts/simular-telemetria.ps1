$ErrorActionPreference = "Continue"
Set-Location (Join-Path $PSScriptRoot "..")

$ka = "9BFZH55L0G8123456"
$ecosport = "9BFZB55P7K8765432"

function Send-Reading([string]$Device, [string]$Vin, [int]$Km, [string]$Description) {
    $now = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    $payload = "{""odometerKm"":$Km,""recordedAt"":""$now""}"
    Write-Host "-> $Description"
    $payload | docker compose run --rm -T veiculo -h mosquitto -p 8883 `
        --cafile /certs/ca.crt --cert "/certs/$Device.crt" --key "/certs/$Device.key" `
        -t "vehicles/$Vin/telemetry" -q 1 -V 5 -s
    Write-Host "   código de saída: $LASTEXITCODE"
}

Send-Reading "veiculo-ka" $ka 72500 "Ka envia leitura válida (71.000 -> 72.500 km): API aceita"
Send-Reading "veiculo-ecosport" $ecosport 50100 "EcoSport envia leitura válida: API aceita"
Send-Reading "veiculo-ka" $ka 60000 "Ka envia hodômetro MENOR (possível adulteração): API ignora e alerta"
Send-Reading "veiculo-ka" $ka 400000 "Ka envia salto de 327 mil km: API rejeita"
Send-Reading "veiculo-ka" $ecosport 99999 "Certificado do Ka tentando publicar no tópico do EcoSport: broker bloqueia pela ACL"

Write-Host "-> Dispositivo SEM certificado tentando conectar: TLS recusa a conexão"
docker compose run --rm -T veiculo -h mosquitto -p 8883 --cafile /certs/ca.crt `
    -t "vehicles/$ka/telemetry" -m "{}" -q 1
Write-Host "   código de saída: $LASTEXITCODE (diferente de 0 = recusado)"

Write-Host ""
Write-Host "Logs do broker (ACL e TLS):"
docker compose logs --tail 15 mosquitto
Write-Host ""
Write-Host "Logs da API (telemetry.*):"
docker compose logs --tail 200 api | Select-String "telemetry\."
