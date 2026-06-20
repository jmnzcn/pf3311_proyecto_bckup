# Upload build zip to S3 and deploy to all pf3311 AWS VMs via SSM
param(
    [string]$ZipPath = "",
    [string]$Region = "us-east-1",
    [string]$Bucket = "pf3311-deploy-138262741072"
)

$ErrorActionPreference = "Stop"
$ScriptDir = $PSScriptRoot
$Root = Split-Path (Split-Path $ScriptDir -Parent) -Parent
$MetaFile = Join-Path $ScriptDir "pf3311_instances.json"

aws sts get-caller-identity --region $Region | Out-Null
if ($LASTEXITCODE -ne 0) { throw "AWS CLI not configured. Run: aws configure" }

if (-not (Test-Path $MetaFile)) { throw "Missing $MetaFile" }
$instances = Get-Content $MetaFile -Raw | ConvertFrom-Json

if ([string]::IsNullOrWhiteSpace($ZipPath)) {
    $ZipPath = Join-Path $Root "Build\pf3311_build.zip"
}
if (-not (Test-Path $ZipPath)) {
    $winDir = Join-Path $Root "Build\Windows"
    if (-not (Test-Path (Join-Path $winDir "ExperimentPrototypeB03230.exe"))) {
        throw "No build zip or exe. Run _tools\build_windows.bat first."
    }
    Write-Host "Zipping Build\Windows -> $ZipPath"
    Remove-Item $ZipPath -Force -ErrorAction SilentlyContinue
    Compress-Archive -Path "$winDir\*" -DestinationPath $ZipPath -CompressionLevel Optimal
}

Write-Host "Uploading to s3://$Bucket/build/pf3311_build.zip ..."
aws s3 cp $ZipPath "s3://$Bucket/build/pf3311_build.zip" --region $Region
if ($LASTEXITCODE -ne 0) { throw "S3 upload failed" }

$url = (aws s3 presign "s3://$Bucket/build/pf3311_build.zip" --expires-in 3600 --region $Region).Trim()
$ssmJson = @{
    commands = @(
        '$ErrorActionPreference = ''Stop'''
        'Get-Process ExperimentPrototypeB03230 -ErrorAction SilentlyContinue | Stop-Process -Force'
        'New-Item -ItemType Directory -Force -Path ''C:\Experimento\CSV data'' | Out-Null'
        "Invoke-WebRequest -Uri '$url' -OutFile 'C:\Windows\Temp\pf3311_build.zip'"
        'Expand-Archive -Path ''C:\Windows\Temp\pf3311_build.zip'' -DestinationPath ''C:\Experimento'' -Force'
        'Remove-Item ''C:\Windows\Temp\pf3311_build.zip'' -Force'
        "Write-Output ('EXE: ' + (Test-Path 'C:\Experimento\ExperimentPrototypeB03230.exe'))"
    )
} | ConvertTo-Json -Depth 3
$tmpJson = Join-Path $env:TEMP "ssm_deploy_build_$(Get-Date -Format 'yyyyMMddHHmmss').json"
[System.IO.File]::WriteAllText($tmpJson, $ssmJson, [System.Text.UTF8Encoding]::new($false))

foreach ($vm in $instances) {
    $id = $vm.InstanceId
    $name = $vm.Name
    Write-Host "Deploying to $name ($id)..."
    $cmdId = (aws ssm send-command `
        --instance-ids $id `
        --document-name "AWS-RunPowerShellScript" `
        --parameters "file://$($tmpJson -replace '\\','/')" `
        --region $Region `
        --query "Command.CommandId" --output text).Trim()
    if ([string]::IsNullOrWhiteSpace($cmdId)) { throw "SSM send-command failed for $name" }

    $deadline = (Get-Date).AddMinutes(5)
    do {
        Start-Sleep -Seconds 5
        $inv = aws ssm get-command-invocation --command-id $cmdId --instance-id $id --region $Region `
            --query "[Status,StandardOutputContent,StandardErrorContent]" --output json | ConvertFrom-Json
        $status = $inv[0]
    } while ($status -in @('Pending', 'InProgress', 'Delayed') -and (Get-Date) -lt $deadline)

    Write-Host "  Status: $status"
    if ($inv[1]) { Write-Host "  $($inv[1])" }
    if ($status -ne 'Success') {
        if ($inv[2]) { Write-Host "  ERR: $($inv[2])" -ForegroundColor Red }
        throw "Deploy failed on $name"
    }
}

Remove-Item $tmpJson -Force -ErrorAction SilentlyContinue
Write-Host ""
Write-Host "=== DEPLOY OK (all VMs) ===" -ForegroundColor Green
$instances | ForEach-Object { Write-Host "  $($_.Name): $($_.DcvUrl)" }
