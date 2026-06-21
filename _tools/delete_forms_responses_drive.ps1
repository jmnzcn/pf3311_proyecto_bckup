# Elimina respuestas de participantes SOLO en Google Forms (vivo).
# DESTRUCTIVO en Forms. El backup en Drive NO se toca.
#
# Uso:
#   powershell -ExecutionPolicy Bypass -File _tools\delete_forms_responses_drive.ps1 -Participants P01
#   powershell -ExecutionPolicy Bypass -File _tools\delete_forms_responses_drive.ps1 -Participants "P01,P02"
param(
    [Parameter(Mandatory = $true, HelpMessage = "Código(s) a eliminar: P01 o P01,P02")]
    [string]$Participants
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$configPath = Join-Path $root "_tools\google_drive\deploy_info.json"
$secretsPath = Join-Path $root "_config\LocalSecrets.json"

if (-not (Test-Path $configPath)) {
    Write-Error "Falta $configPath. Desplegá primero el Apps Script."
}

$deploy = Get-Content $configPath -Raw | ConvertFrom-Json
$url = [string]$deploy.deployUrl
if ([string]::IsNullOrWhiteSpace($url)) {
    Write-Error "deployUrl vacío en deploy_info.json"
}

$secret = $null
if (Test-Path $secretsPath) {
    $secrets = Get-Content $secretsPath -Raw | ConvertFrom-Json
    $secret = [string]$secrets.csvDriveUploadSecret
}
if ([string]::IsNullOrWhiteSpace($secret)) {
    $secret = [string]$deploy.uploadSecret
}
if ([string]::IsNullOrWhiteSpace($secret)) {
    Write-Error "No se encontró csvDriveUploadSecret (LocalSecrets.json o deploy_info.json)."
}

$codes = ($Participants -split ',') | ForEach-Object { $_.Trim() } | Where-Object { $_ }
if ($codes.Count -eq 0) {
    Write-Error "Indicá al menos un código (ej. -Participants P01 o -Participants 'P01,P02')."
}

$encodedParticipants = [uri]::EscapeDataString(($codes -join ','))
$uri = "$url`?secret=$secret&action=deleteFormResponses&participants=$encodedParticipants&confirm=1"

Write-Host "ATENCIÓN: se eliminarán respuestas de: $($codes -join ', ')"
Write-Host "Solo Google Forms (backup Drive intacto)"
Write-Host $uri.Replace($secret, "***")
Write-Host ""

$response = Invoke-RestMethod -Uri $uri -Method Get -TimeoutSec 180
$response | ConvertTo-Json -Depth 8

if (-not $response.ok) {
    Write-Error "Eliminación falló: $($response.error)"
}

Write-Host ""
Write-Host "Listo. Revisá las respuestas en Google Forms."
