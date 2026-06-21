# Fusiona exports de Forms data/ al backup Forms data/_produccion/ (solo agrega filas nuevas).
# Uso:
#   powershell -ExecutionPolicy Bypass -File _tools\backup_forms_produccion.ps1
#   powershell -ExecutionPolicy Bypass -File _tools\backup_forms_produccion.ps1 -SyncDrive
param(
    [switch]$SyncDrive
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    if ($SyncDrive) {
        python "_tools\backup_forms_produccion.py" merge --sync-drive
    }
    else {
        python "_tools\backup_forms_produccion.py" merge
    }
}
finally {
    Pop-Location
}
