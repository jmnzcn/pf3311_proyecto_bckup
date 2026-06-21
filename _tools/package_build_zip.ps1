# Empaqueta Build/Windows/ para compartir (Drive, VM, evaluador).
# Ejecutar DESPUÉS de build_windows.bat o Build Profiles en Unity.
param(
    [string]$OutZip = "Build\ExperimentPrototypeB03230_Windows.zip"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$winDir = Join-Path $root "Build\Windows"
$exe = Join-Path $winDir "ExperimentPrototypeB03230.exe"
$secrets = Join-Path $winDir "_config\LocalSecrets.json"

if (-not (Test-Path $exe)) {
    Write-Error "No existe $exe. Generá el build primero (_tools\build_windows.bat con Unity cerrado)."
}

if (-not (Test-Path $secrets)) {
    Write-Error "Falta $secrets. Copiá _config\LocalSecrets.json al build o regenerá con StandaloneBuild."
}

$dest = Join-Path $root $OutZip
if (Test-Path $dest) { Remove-Item $dest -Force }

Compress-Archive -Path (Join-Path $winDir "*") -DestinationPath $dest -CompressionLevel Optimal
$mb = [math]::Round((Get-Item $dest).Length / 1MB, 1)
Write-Host "Listo: $dest ($mb MB)"
Write-Host "Subí este zip a Drive con enlace restringido (solo personas invitadas)."
