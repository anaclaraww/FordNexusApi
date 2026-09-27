$ErrorActionPreference = "Stop"
$base = "http://localhost:8080/api/v1"

function Invoke-Api([string]$Method, [string]$Path, $Body = $null, [string]$Token = $null) {
    $headers = @{}
    if ($Token) { $headers.Authorization = "Bearer $Token" }
    $params = @{ Method = $Method; Uri = "$base$Path"; Headers = $headers; ContentType = "application/json"; UseBasicParsing = $true }
    if ($null -ne $Body) { $params.Body = ($Body | ConvertTo-Json -Compress) }
    try {
        $response = Invoke-WebRequest @params
        $content = if ($response.Content) { $response.Content | ConvertFrom-Json } else { $null }
        return [pscustomobject]@{ Status = [int]$response.StatusCode; Body = $content }
    }
    catch {
        if ($_.Exception.Response) { return [pscustomobject]@{ Status = [int]$_.Exception.Response.StatusCode; Body = $null } }
        throw
    }
}

function Get-Token([string]$Email, [string]$Password) {
    $r = Invoke-Api POST "/auth/login" @{ email = $Email; password = $Password }
    if ($r.Status -ne 200) { throw "Login de $Email falhou com $($r.Status)" }
    return $r.Body.accessToken
}

Write-Host "1. Logins legítimos (4 perfis)"
$admin = Get-Token "admin@fordnexus.com" "Admin@123"
$dealer = Get-Token "concessionaria@fordnexus.com" "Dealer@123"
$campinas = Get-Token "campinas@fordnexus.com" "Dealer@123"
$partner = Get-Token "seguradora@fordnexus.com" "Parceiro@123"

Write-Host "2. Uso normal: oficinas, fila de manutenção, agendamento"
1..30 | ForEach-Object { Invoke-Api GET "/workshops" | Out-Null }
$queue = Invoke-Api GET "/dealerships/11111111-1111-1111-1111-111111111111/maintenance-queue" -Token $dealer
Write-Host "   fila: $($queue.Body.totalDue) veículos"
$when = (Get-Date).AddDays(7).ToString("yyyy-MM-ddT09:00:00-03:00")
$appointment = Invoke-Api POST "/appointments" @{ vin = "9BFZB55P7K8765432"; serviceType = "Revision"; scheduledAt = $when } -Token $dealer
Write-Host "   agendamento: $($appointment.Status)"

Write-Host "3. Eventos de auditoria: parceiro lê histórico, Admin muda certificação e exclui veículo"
Invoke-Api GET "/vehicles/9BFZH54S8J8975310/history" -Token $partner | Out-Null
Invoke-Api PUT "/workshops/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/certification" @{ status = "Suspended" } -Token $admin | Out-Null
$vin = "9BFZZ99Z9T" + (Get-Random -Minimum 1000000 -Maximum 9999999)
Invoke-Api POST "/vehicles" @{ vin = $vin; model = "Maverick"; modelYear = 2024; currentMileage = 100; ownerName = "Teste Auditoria"; ownerPhone = "+5511999990000"; homeDealershipId = "11111111-1111-1111-1111-111111111111" } -Token $admin | Out-Null
Invoke-Api DELETE "/vehicles/$vin" -Token $admin | Out-Null

Write-Host "4. Acessos indevidos: Campinas tentando ler dados de Sorocaba, parceiro listando veículos"
1..4 | ForEach-Object {
    $r = Invoke-Api GET "/dealerships/11111111-1111-1111-1111-111111111111/maintenance-queue" -Token $campinas
    Write-Host "   Campinas -> fila de Sorocaba: $($r.Status)"
}
$r = Invoke-Api GET "/vehicles" -Token $partner
Write-Host "   Parceiro -> /vehicles: $($r.Status)"

Write-Host "5. Aguardando 61 s para a janela de rate limit do login reiniciar..."
Start-Sleep -Seconds 61

Write-Host "6. Força bruta na conta da seguradora"
1..8 | ForEach-Object {
    $r = Invoke-Api POST "/auth/login" @{ email = "seguradora@fordnexus.com"; password = "Chute$_@2026" }
    Write-Host "   tentativa $_ : $($r.Status)"
}

Write-Host ""
Write-Host "Pronto. Veja o dashboard no Grafana e os alertas em http://localhost:9090/alerts"
Write-Host "Trilha de auditoria: GET $base/audit-events (token de Admin)"
