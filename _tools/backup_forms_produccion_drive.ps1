# Ejecuta backup Drive → Drive de encuestas (Forms en Google → CSV en carpeta producción).
# Requiere Apps Script desplegado con FormsBackup.gs (ver COMO_BACKUP_FORMS_DRIVE.txt).
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$configPath = Join-Path $root "_tools\google_drive\deploy_info.json"
$secretsPath = Join-Path $root "_config\LocalSecrets.json"

if (-not (Test-Path $configPath)) {
    Write-Error "Falta $configPath. Desplegá primero el Apps Script (_tools\google_drive\deploy_drive_uploader.ps1)."
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

$uri = "$url`?secret=$secret&action=backupFormsProduccion"
Write-Host "Backup encuestas: Forms (Google) -> Drive producción"
Write-Host $uri.Replace($secret, "***")

$response = Invoke-RestMethod -Uri $uri -Method Get -TimeoutSec 120
$response | ConvertTo-Json -Depth 6

if (-not $response.ok) {
    Write-Error "Backup falló: $($response.error)"
}

Write-Host ""
Write-Host "Listo. Revisá la carpeta:"
Write-Host "https://drive.google.com/drive/folders/1RLLESYhJbJkBOnNdV6uZTaehl-t-W-Cp"
