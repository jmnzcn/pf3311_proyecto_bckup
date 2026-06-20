# Submit Azure GPU quota increase request (Standard NCASv3 T4 + regional vCPUs)
param(
    [string]$Location = "eastus",
    [int]$NcAsv3Limit = 16,
    [int]$RegionalLimit = 20,
    [string]$SubscriptionId = ""
)

$ErrorActionPreference = "Stop"
$Az = "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd"
$ScriptDir = $PSScriptRoot

function Invoke-AzJson {
    param([string[]]$AzArgs)
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $out = & $Az @AzArgs 2>&1
    $code = $LASTEXITCODE
    $ErrorActionPreference = $prev
    if ($code -ne 0) { throw ($out | Out-String) }
    return ($out | Out-String).Trim()
}

if ([string]::IsNullOrWhiteSpace($SubscriptionId)) {
    $SubscriptionId = (Invoke-AzJson @("account", "show", "--query", "id", "-o", "tsv"))
}
$scope = "/subscriptions/$SubscriptionId/providers/Microsoft.Compute/locations/$Location"

Write-Host "Subscription: $SubscriptionId"
Write-Host "Scope: $scope"
Write-Host "Registering Microsoft.Quota (if needed)..."
& $Az provider register --namespace Microsoft.Quota --wait | Out-Null

Write-Host ""
Write-Host "Current quotas:"
Invoke-AzJson @("quota", "show", "--scope", $scope, "--resource-name", "Standard NCASv3_T4 Family", "--query", "properties.limit.value", "-o", "tsv") | ForEach-Object { Write-Host "  NCASv3_T4 Family: $_" }
Invoke-AzJson @("quota", "show", "--scope", $scope, "--resource-name", "cores", "--query", "properties.limit.value", "-o", "tsv") | ForEach-Object { Write-Host "  Total Regional vCPUs (cores): $_" }

$notesFile = Join-Path $ScriptDir "quota_request_ncasv3.json"
if (-not (Test-Path $notesFile)) {
    @{ properties = @{ notes = "PF-3311 UX experiment: 3x Standard_NC4as_T4_v3 Windows VMs, Unity 3D + NICE DCV. GPU on all VMs." } } |
        ConvertTo-Json -Depth 3 | Set-Content $notesFile -Encoding utf8
}

function Request-Quota {
    param([string]$ResourceName, [int]$NewLimit)
    Write-Host ""
    Write-Host "Requesting '$ResourceName' -> $NewLimit ..."
    $result = Invoke-AzJson @(
        "quota", "update",
        "--resource-name", $ResourceName,
        "--scope", $scope,
        "--limit-object", "value=$NewLimit",
        "--properties", "@$notesFile",
        "--no-wait",
        "-o", "json"
    )
    Write-Host $result
}

Request-Quota -ResourceName "Standard NCASv3_T4 Family" -NewLimit $NcAsv3Limit
Request-Quota -ResourceName "cores" -NewLimit $RegionalLimit

Write-Host ""
Write-Host "Quota requests submitted (async). Check status:"
Write-Host "  az quota request list --scope `"$scope`" -o table"
Write-Host ""
Start-Sleep -Seconds 5
& $Az quota request list --scope $scope -o table 2>&1
