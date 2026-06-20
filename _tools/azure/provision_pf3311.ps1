# PF-3311 — Provision 3 Windows GPU VMs on Azure (eastus) + NSG + storage for deploy
# Prerequisites: az login
param(
    [string]$Location = "eastus",
    [string]$ResourceGroup = "pf3311-rg",
    [string]$VmSize = "Standard_NC4as_T4_v3",
    [int]$Count = 3,
    [string]$ProjectPrefix = "pf3311",
    [string]$AdminUser = "pf3311admin",
    [string]$AdminPassword = ""
)

$ErrorActionPreference = "Stop"
$Az = "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd"
$ScriptDir = $PSScriptRoot

function Invoke-Az {
    param(
        [string[]]$AzArgs,
        [switch]$AllowNotFound
    )
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $out = & $Az @AzArgs 2>&1
    $code = $LASTEXITCODE
    $ErrorActionPreference = $prev
    if ($code -ne 0) {
        $text = ($out | Out-String)
        if ($AllowNotFound -and ($text -match 'ResourceNotFound|NotFound|not found|was not found')) { return $null }
        throw "az failed: az $($AzArgs -join ' ')`n$text"
    }
    return $out
}

Invoke-Az @("account", "show", "-o", "none") | Out-Null
$sub = (Invoke-Az @("account", "show", "--query", "id", "-o", "tsv") | Out-String).Trim()
Write-Host "Subscription: $sub"
Write-Host "Location: $Location | Size: $VmSize | VMs: $Count"

if ([string]::IsNullOrWhiteSpace($AdminPassword)) {
    $credFile = Join-Path $ScriptDir "pf3311_vm_credentials.json"
    if (Test-Path $credFile) {
        $saved = Get-Content $credFile -Raw | ConvertFrom-Json
        $AdminPassword = $saved.password
        $AdminUser = $saved.username
        Write-Host "Using saved credentials for user: $AdminUser"
    } else {
        $AdminPassword = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 20 | ForEach-Object { [char]$_ })
        @{ username = $AdminUser; password = $AdminPassword } | ConvertTo-Json | Set-Content $credFile -Encoding utf8
        Write-Host "Generated VM password -> $credFile (DO NOT COMMIT)"
    }
}

$nsgName = "$ProjectPrefix-nsg"
$vnetName = "$ProjectPrefix-vnet"
$subnetName = "default"

Invoke-Az @("group", "create", "--name", $ResourceGroup, "--location", $Location, "-o", "none")

$nsgExists = Invoke-Az @("network", "nsg", "show", "--resource-group", $ResourceGroup, "--name", $nsgName, "-o", "tsv", "--query", "name") -AllowNotFound
if (-not $nsgExists) {
    Invoke-Az @("network", "nsg", "create", "--resource-group", $ResourceGroup, "--name", $nsgName, "-o", "none")
    Invoke-Az @("network", "nsg", "rule", "create", "--resource-group", $ResourceGroup, "--nsg-name", $nsgName,
        "--name", "DCV-TCP-8443", "--priority", "1000", "--access", "Allow", "--direction", "Inbound",
        "--protocol", "Tcp", "--destination-port-range", "8443", "--source-address-prefix", "*", "-o", "none")
    Invoke-Az @("network", "nsg", "rule", "create", "--resource-group", $ResourceGroup, "--nsg-name", $nsgName,
        "--name", "DCV-UDP-8443", "--priority", "1001", "--access", "Allow", "--direction", "Inbound",
        "--protocol", "Udp", "--destination-port-range", "8443", "--source-address-prefix", "*", "-o", "none")
    Invoke-Az @("network", "nsg", "rule", "create", "--resource-group", $ResourceGroup, "--nsg-name", $nsgName,
        "--name", "RDP-3389", "--priority", "1002", "--access", "Allow", "--direction", "Inbound",
        "--protocol", "Tcp", "--destination-port-range", "3389", "--source-address-prefix", "*", "-o", "none")
}

$vnetExists = Invoke-Az @("network", "vnet", "show", "--resource-group", $ResourceGroup, "--name", $vnetName, "-o", "tsv", "--query", "name") -AllowNotFound
if (-not $vnetExists) {
    Invoke-Az @("network", "vnet", "create", "--resource-group", $ResourceGroup, "--name", $vnetName,
        "--address-prefix", "10.50.0.0/16", "--subnet-name", $subnetName, "--subnet-prefix", "10.50.1.0/24", "-o", "none")
}

$subClean = ($sub -replace '[^a-z0-9]', '').ToLower()
if ($subClean.Length -lt 4) { $subClean = "lab$((Get-Random -Maximum 999999))" }
$storageName = ("pf3311deploy" + $subClean.Substring(0, [Math]::Min(14, $subClean.Length))).Substring(0, [Math]::Min(24, 7 + [Math]::Min(14, $subClean.Length)))
$saExists = Invoke-Az @("storage", "account", "show", "--name", $storageName, "-o", "tsv", "--query", "name") -AllowNotFound
if (-not $saExists) {
    Invoke-Az @("storage", "account", "create", "--name", $storageName, "--resource-group", $ResourceGroup,
        "--location", $Location, "--sku", "Standard_LRS", "-o", "none")
}
Write-Host "Storage account: $storageName"

$image = "MicrosoftWindowsServer:WindowsServer:2022-datacenter-azure-edition:latest"
$summary = @()

for ($i = 1; $i -le $Count; $i++) {
    $vmName = "$ProjectPrefix-vm$i"
    $pipName = "$vmName-pip"

    $vmExists = Invoke-Az @("vm", "show", "--resource-group", $ResourceGroup, "--name", $vmName, "-o", "tsv", "--query", "name") -AllowNotFound
    if ($vmExists) {
        Write-Host "VM $vmName already exists, skipping create."
    } else {
        Write-Host "Creating $vmName ($VmSize)..."
        Invoke-Az @(
            "vm", "create",
            "--resource-group", $ResourceGroup,
            "--name", $vmName,
            "--location", $Location,
            "--size", $VmSize,
            "--image", $image,
            "--admin-username", $AdminUser,
            "--admin-password", $AdminPassword,
            "--public-ip-sku", "Standard",
            "--public-ip-address", $pipName,
            "--nsg", $nsgName,
            "--vnet-name", $vnetName,
            "--subnet", $subnetName,
            "--os-disk-size-gb", "128",
            "--storage-sku", "Premium_LRS",
            "-o", "none"
        )
        Write-Host "Installing NVIDIA GPU driver extension..."
        Invoke-Az @(
            "vm", "extension", "set",
            "--resource-group", $ResourceGroup,
            "--vm-name", $vmName,
            "--name", "NvidiaGpuDriverWindows",
            "--publisher", "Microsoft.HpcCompute",
            "--version", "1.6",
            "-o", "none"
        )
    }

    $ip = (Invoke-Az @("vm", "show", "-d", "--resource-group", $ResourceGroup, "--name", $vmName,
        "--query", "publicIps", "-o", "tsv")).Trim()
    $summary += [PSCustomObject]@{
        Name       = $vmName
        ResourceGroup = $ResourceGroup
        PublicIp   = $ip
        DcvUrl     = "https://${ip}:8443"
        AdminUser  = $AdminUser
    }
}

$meta = @{
    subscriptionId = $sub
    resourceGroup  = $ResourceGroup
    location       = $Location
    storageAccount = $storageName
    adminUser      = $AdminUser
    vms            = $summary
}
$outFile = Join-Path $ScriptDir "pf3311_instances.json"
$meta | ConvertTo-Json -Depth 5 | Set-Content $outFile -Encoding utf8

Write-Host ""
Write-Host "=== AZURE PF-3311 LISTO ===" -ForegroundColor Green
$summary | Format-Table -AutoSize
Write-Host "Metadata: $outFile"
Write-Host "Credentials: $(Join-Path $ScriptDir 'pf3311_vm_credentials.json')"
Write-Host ""
Write-Host "Siguiente:" -ForegroundColor Yellow
Write-Host "  .\_tools\azure\install_dcv_all.ps1"
Write-Host "  .\_tools\azure\deploy_build.ps1"
