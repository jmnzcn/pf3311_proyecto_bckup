# Copia sesiones nuevas de CSV data/ al backup de producción sin sobrescribir carpetas existentes.
# Cada sesión es P##_ID-YYYYMMDD.../ — el SessionID evita pisar datos reales al probar de nuevo.
param(
    [string]$FromDir = "CSV data",
    [string]$IntoDir = "CSV data\_produccion"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$source = Join-Path $root $FromDir
$dest = Join-Path $root $IntoDir

if (-not (Test-Path $source)) {
    Write-Error "No existe carpeta origen: $source"
}

New-Item -ItemType Directory -Force -Path $dest | Out-Null

$added = 0
$skipped = 0

Get-ChildItem $source -Directory | ForEach-Object {
    $name = $_.Name
    if ($name -match '^P\d+_ID-') {
        $target = Join-Path $dest $name
        if (Test-Path $target) {
            $skipped++
            Write-Host "  ya existe (no se toca): $name"
        }
        else {
            Copy-Item $_.FullName $target -Recurse
            $added++
            Write-Host "  copiada: $name"
        }
    }
}

Write-Host ""
Write-Host "Backup CSV data: +$added sesiones nuevas, $skipped ya presentes."
Write-Host "Destino: $dest"
