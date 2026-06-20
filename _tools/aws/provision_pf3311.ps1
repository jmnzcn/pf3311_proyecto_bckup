# PF-3311 — Create 3 Windows GPU instances + security group + Elastic IPs (us-east-1)
# Prerequisites: AWS CLI configured (aws sts get-caller-identity works)
param(
    [string]$Region = "us-east-1",
    [string]$InstanceType = "g4dn.xlarge",
    [int]$Count = 3,
    [string]$KeyName = "pf3311-key",
    [string]$ProjectPrefix = "pf3311"
)

$ErrorActionPreference = "Stop"
if (Get-Variable -Name PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue) {
    $PSNativeCommandUseErrorActionPreference = $false
}
$Root = Split-Path -Parent $PSScriptRoot
$UserDataFile = Join-Path $PSScriptRoot "windows_user_data.ps1"

function Require-Aws {
    $env:AWS_DEFAULT_REGION = $Region
    aws sts get-caller-identity | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "AWS CLI sin credenciales. En PowerShell ejecuta:" -ForegroundColor Yellow
        Write-Host "  aws login" -ForegroundColor Cyan
        Write-Host "o crea access keys en IAM y:" -ForegroundColor Yellow
        Write-Host "  aws configure" -ForegroundColor Cyan
        exit 1
    }
}

Require-Aws

Write-Host "Region: $Region | Type: $InstanceType | VMs: $Count"

# Latest Windows Server 2022 English Full Base
$ami = aws ec2 describe-images `
    --owners amazon `
    --filters "Name=name,Values=Windows_Server-2022-English-Full-Base-*" "Name=state,Values=available" `
    --query "sort_by(Images,&CreationDate)[-1].ImageId" `
    --output text `
    --region $Region
Write-Host "AMI: $ami"

# Key pair (create if missing)
$keyList = aws ec2 describe-key-pairs --region $Region --query "KeyPairs[?KeyName=='$KeyName'].KeyName" --output text
if ([string]::IsNullOrWhiteSpace($keyList)) {
    $keyPath = Join-Path $PSScriptRoot "$KeyName.pem"
    aws ec2 create-key-pair --key-name $KeyName --query KeyMaterial --output text --region $Region | Out-File -FilePath $keyPath -Encoding ascii
    Write-Host "Key pair saved: $keyPath"
} else {
    Write-Host "Key pair '$KeyName' already exists."
}

# Security group
$sgName = "$ProjectPrefix-dcv"
$vpcId = aws ec2 describe-vpcs --filters "Name=isDefault,Values=true" --query "Vpcs[0].VpcId" --output text --region $Region
$sgList = aws ec2 describe-security-groups --filters "Name=group-name,Values=$sgName" "Name=vpc-id,Values=$vpcId" --query "SecurityGroups[0].GroupId" --output text --region $Region
$sgId = $sgList
if ([string]::IsNullOrWhiteSpace($sgId) -or $sgId -eq "None") {
    $sgId = aws ec2 create-security-group --group-name $sgName --description "PF3311 NICE DCV" --vpc-id $vpcId --query GroupId --output text --region $Region
    aws ec2 authorize-security-group-ingress --group-id $sgId --protocol tcp --port 8443 --cidr 0.0.0.0/0 --region $Region | Out-Null
    aws ec2 authorize-security-group-ingress --group-id $sgId --protocol udp --port 8443 --cidr 0.0.0.0/0 --region $Region | Out-Null
    aws ec2 authorize-security-group-ingress --group-id $sgId --protocol tcp --port 3389 --cidr 0.0.0.0/0 --region $Region | Out-Null
    Write-Host "Security group created: $sgId"
} else {
    Write-Host "Security group exists: $sgId"
}

$userDataArg = "file://$($UserDataFile -replace '\\','/')"

$instanceIds = @()
for ($i = 1; $i -le $Count; $i++) {
    $name = "$ProjectPrefix-vm$i"
    Write-Host "Launching $name..."
    $id = aws ec2 run-instances `
        --image-id $ami `
        --instance-type $InstanceType `
        --key-name $KeyName `
        --security-group-ids $sgId `
        --block-device-mappings "DeviceName=/dev/sda1,Ebs={VolumeSize=100,VolumeType=gp3}" `
        --user-data $userDataArg `
        --tag-specifications "ResourceType=instance,Tags=[{Key=Name,Value=$name}]" `
        --query "Instances[0].InstanceId" `
        --output text `
        --region $Region
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($id)) {
        Write-Host "ERROR: failed to launch $name (see AWS message above)." -ForegroundColor Red
        exit 1
    }
    $instanceIds += $id
    Write-Host "  InstanceId: $id"
}

Write-Host "Waiting for instances to run (may take 10-15 min)..."
aws ec2 wait instance-running --instance-ids $instanceIds --region $Region

$summary = @()
foreach ($id in $instanceIds) {
    $name = aws ec2 describe-tags --filters "Name=resource-id,Values=$id" "Name=key,Values=Name" --query "Tags[0].Value" --output text --region $Region
    $eipAlloc = aws ec2 allocate-address --domain vpc --query AllocationId --output text --region $Region
    aws ec2 associate-address --instance-id $id --allocation-id $eipAlloc --region $Region | Out-Null
    $publicIp = aws ec2 describe-addresses --allocation-ids $eipAlloc --query "Addresses[0].PublicIp" --output text --region $Region
    $summary += [PSCustomObject]@{
        Name = $name
        InstanceId = $id
        PublicIp = $publicIp
        DcvUrl = "https://${publicIp}:8443"
    }
}

$outFile = Join-Path $PSScriptRoot "pf3311_instances.json"
$summary | ConvertTo-Json -Depth 3 | Set-Content -Path $outFile -Encoding utf8

Write-Host ""
Write-Host "=== LISTO ===" -ForegroundColor Green
$summary | Format-Table -AutoSize
Write-Host "Saved: $outFile"
Write-Host ""
Write-Host "Siguiente (vos, ~15 min por VM):" -ForegroundColor Yellow
Write-Host "  1. Get Windows password: EC2 -> Connect -> RDP -> Get password (usa $KeyName.pem)"
Write-Host "  2. RDP -> subir Build/Windows zip a C:\Experimento\"
Write-Host "  3. Probar enlace DCV desde Chrome"
Write-Host ""
Write-Host "Apagar VMs: aws ec2 stop-instances --instance-ids $($instanceIds -join ' ') --region $Region"
