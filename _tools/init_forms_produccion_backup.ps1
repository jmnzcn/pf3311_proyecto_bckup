# Crea la carpeta de backup de producción para encuestas (Forms data/_produccion).
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    python "_tools\backup_forms_produccion.py" init
}
finally {
    Pop-Location
}
