# Install NICE DCV on all pf3311 Azure VMs via Run Command
$ErrorActionPreference = "Stop"
$Az = "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd"
$ScriptDir = $PSScriptRoot
$MetaFile = Join-Path $ScriptDir "pf3311_instances.json"
$InstallScript = Join-Path $ScriptDir "install_dcv_on_vm.ps1"

if (-not (Test-Path $MetaFile)) {
    Write-Error "Run provision_pf3311.ps1 first."
}

$meta = Get-Content $MetaFile -Raw | ConvertFrom-Json
$rg = $meta.resourceGroup
$adminUser = $meta.adminUser
$scriptBody = Get-Content $InstallScript -Raw
$scriptBody = $scriptBody -replace 'Administrator', $adminUser

foreach ($vm in $meta.vms) {
    $name = $vm.Name
    Write-Host "Installing DCV on $name..."
    $tmp = [System.IO.Path]::GetTempFileName() + ".ps1"
    Set-Content -Path $tmp -Value $scriptBody -Encoding utf8
    & $Az vm run-command invoke `
        --resource-group $rg `
        --name $name `
        --command-id RunPowerShellScript `
        --scripts "@$tmp" `
        --query "value[0].message" -o tsv
    Remove-Item $tmp -Force -ErrorAction SilentlyContinue
    Write-Host "Done: $name"
}

Write-Host "DCV install finished on all VMs."
