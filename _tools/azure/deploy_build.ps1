# Upload build zip to Azure Storage and deploy to all pf3311 VMs
param(
    [string]$ZipPath = ""
)

$ErrorActionPreference = "Stop"
$Az = "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd"
$ScriptDir = $PSScriptRoot
$Root = Split-Path (Split-Path $ScriptDir -Parent) -Parent
$MetaFile = Join-Path $ScriptDir "pf3311_instances.json"

if (-not (Test-Path $MetaFile)) { Write-Error "Run provision_pf3311.ps1 first." }
$meta = Get-Content $MetaFile -Raw | ConvertFrom-Json
$rg = $meta.resourceGroup
$sa = $meta.storageAccount

if ([string]::IsNullOrWhiteSpace($ZipPath)) {
    $ZipPath = Join-Path $Root "Build\pf3311_build.zip"
}
if (-not (Test-Path $ZipPath)) {
    Write-Error "Build zip not found: $ZipPath. Run build_windows.bat first."
}

Write-Host "Uploading $ZipPath to storage $sa..."
& $Az storage blob upload --account-name $sa --container-name build --name pf3311_build.zip --file $ZipPath --auth-mode login --overwrite 2>$null
if ($LASTEXITCODE -ne 0) {
    & $Az storage container create --account-name $sa --name build --auth-mode login -o none
    & $Az storage blob upload --account-name $sa --container-name build --name pf3311_build.zip --file $ZipPath --auth-mode login --overwrite
}

$expiry = (Get-Date).ToUniversalTime().AddHours(4).ToString("yyyy-MM-ddTHH:mm:ssZ")
$url = (& $Az storage blob generate-sas --account-name $sa --container-name build --name pf3311_build.zip `
    --permissions r --expiry $expiry --auth-mode login -o tsv).Trim()
$blobUrl = "https://${sa}.blob.core.windows.net/build/pf3311_build.zip?$url"

$deployScript = @"
`$ErrorActionPreference = 'Stop'
Get-Process ExperimentPrototypeB03230 -ErrorAction SilentlyContinue | Stop-Process -Force
New-Item -ItemType Directory -Force -Path 'C:\Experimento\CSV data' | Out-Null
Invoke-WebRequest -Uri '$blobUrl' -OutFile 'C:\Windows\Temp\pf3311_build.zip'
Expand-Archive -Path 'C:\Windows\Temp\pf3311_build.zip' -DestinationPath 'C:\Experimento' -Force
Remove-Item 'C:\Windows\Temp\pf3311_build.zip' -Force
Write-Output ('EXE: ' + (Test-Path 'C:\Experimento\ExperimentPrototypeB03230.exe'))
"@

foreach ($vm in $meta.vms) {
    $name = $vm.Name
    Write-Host "Deploying to $name..."
    $tmp = [System.IO.Path]::GetTempFileName() + ".ps1"
    Set-Content -Path $tmp -Value $deployScript -Encoding utf8
    & $Az vm run-command invoke --resource-group $rg --name $name --command-id RunPowerShellScript --scripts "@$tmp" --query "value[0].message" -o tsv
    Remove-Item $tmp -Force -ErrorAction SilentlyContinue
}

Write-Host "Deploy complete."
