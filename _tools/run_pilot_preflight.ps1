# Preflight piloto PF-3311 - ejecutar desde la raiz del repo
param(
    [switch]$Build,
    [switch]$Deploy,
    [string]$CsvDir = "CSV data"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path $PSScriptRoot -Parent
Set-Location $Root

Write-Host "=== PF-3311 preflight ===" -ForegroundColor Cyan

Write-Host ""
Write-Host "[1/4] Banco de preguntas..."
python _tools/export_question_bank.py
if ($LASTEXITCODE -ne 0) { throw "export_question_bank failed" }

Write-Host ""
Write-Host "[2/4] Forms (config)..."
$config = "_tools/data/pf3311_forms_config.json"
if (-not (Test-Path $config)) { throw "Missing $config" }
Write-Host "  Ver URLs en $config"
Write-Host "  Crear Forms con docs/google_forms/apps_script/ si aun no existen."

if (Test-Path $CsvDir) {
    Write-Host ""
    Write-Host "[3/4] Verificar ultima sesion CSV..."
    python _tools/verify_smoke_session.py "$CsvDir" --pilot-rules
    $smokeCode = $LASTEXITCODE
} else {
    Write-Host ""
    Write-Host "[3/4] Sin carpeta CSV - omitiendo verify_smoke_session"
    $smokeCode = 0
}

if ($Build) {
    Write-Host ""
    Write-Host "[4/4] Build Windows..."
    cmd /c _tools\build_windows.bat
    if ($LASTEXITCODE -ne 0) { throw "build_windows.bat failed" }
} else {
    Write-Host ""
    Write-Host "[4/4] Build omitido (use -Build para compilar Unity)"
}

if ($Deploy) {
    if (-not (Test-Path "Build\Windows\ExperimentPrototypeB03230.exe")) {
        throw "No hay build. Ejecute con -Build primero."
    }
    Write-Host ""
    Write-Host "[Deploy] AWS SSM..."
    & _tools\aws\deploy_build.ps1
}

Write-Host ""
Write-Host "=== Listo ===" -ForegroundColor Green
Write-Host "Humo manual en VM: README.md seccion Prueba de humo antes del piloto"
Write-Host ('Tras sesion: python _tools/verify_smoke_session.py "' + $CsvDir + '" --pilot-rules')
Write-Host ('Analisis: python _tools/analyze_all_rq.py "' + $CsvDir + '" --forms-dir "Forms data"')

if ($smokeCode -ne 0) { exit $smokeCode }
exit 0
