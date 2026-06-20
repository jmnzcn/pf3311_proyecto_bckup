# Deploy upload_session_csv.gs to Google Apps Script and write URL + secret into Unity.
param(
    [string]$Secret = "",
    [switch]$SkipUnityUpdate
)

$ErrorActionPreference = "Stop"
$ScriptDir = $PSScriptRoot
$Root = Split-Path (Split-Path $ScriptDir -Parent) -Parent
$ClaspDir = Join-Path $ScriptDir "clasp_project"
$UnityScene = Join-Path $Root "Assets\Scenes\SampleScene.unity"
$SecretFile = Join-Path $ScriptDir "upload_secret.txt"
$DeployInfoFile = Join-Path $ScriptDir "deploy_info.json"

if ([string]::IsNullOrWhiteSpace($Secret)) {
    if (Test-Path $SecretFile) {
        $Secret = (Get-Content $SecretFile -Raw).Trim()
    } else {
        $Secret = [guid]::NewGuid().ToString("N")
    }
}

Set-Content -Path $SecretFile -Value $Secret -Encoding UTF8 -NoNewline

$gsTemplate = Get-Content (Join-Path $ScriptDir "upload_session_csv.gs") -Raw
$formTemplate = Get-Content (Join-Path $ScriptDir "check_form_response.gs") -Raw
$gsBody = $gsTemplate.Replace("var UPLOAD_SECRET = '__UPLOAD_SECRET__';", "var UPLOAD_SECRET = '$Secret';")
$formBody = $formTemplate -replace "function jsonResponse_\(", "function jsonResponse("
$formBody = $formBody -replace "UPLOAD_SECRET === '__UPLOAD_SECRET__'", "UPLOAD_SECRET === UPLOAD_SECRET_UNSET"
$gsBody = ($gsBody.TrimEnd() + "`r`n`r`n" + ($formBody -replace '(?s)^/\*\*.*?\*/\r?\n', '')).TrimEnd() + "`r`n"
Set-Content -Path (Join-Path $ClaspDir "Code.gs") -Value $gsBody -Encoding UTF8

if (-not (Get-Command clasp -ErrorAction SilentlyContinue)) {
    throw "clasp not installed. Run: npm install -g @google/clasp"
}

Push-Location $ClaspDir
try {
    $loggedIn = $false
    try {
        clasp list 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) { $loggedIn = $true }
    } catch {
        $loggedIn = $false
    }

    if (-not $loggedIn) {
        Write-Host "Open the browser and authorize clasp with your Google account (Drive owner)." -ForegroundColor Yellow
        clasp login --no-localhost
        if ($LASTEXITCODE -ne 0) { throw "clasp login failed" }
    }

    if (-not (Test-Path ".clasp.json")) {
        Write-Host "Creating Apps Script project..."
        clasp create --type standalone --title "PF3311 CSV Upload" --rootDir .
        if ($LASTEXITCODE -ne 0) { throw "clasp create failed" }
    }

    Write-Host "Pushing code..."
    clasp push -f
    if ($LASTEXITCODE -ne 0) { throw "clasp push failed" }

    Write-Host "Deploying web app..."
    $deployOut = clasp deploy --description "PF3311 CSV web app" 2>&1 | Out-String
    Write-Host $deployOut

    $deploymentId = $null
    if ($deployOut -match "Deployed (AKfycb[a-zA-Z0-9_-]+)") {
        $deploymentId = $Matches[1]
    }

    if (-not $deploymentId) {
        $deploymentsJson = clasp deployments --json 2>&1 | Out-String
        if ($deploymentsJson) {
            try {
                $deployments = $deploymentsJson | ConvertFrom-Json
                $webDeployment = $deployments.deployments | Where-Object {
                    $_.description -match "PF3311" -and $_.deploymentId -match "^AKfycb"
                } | Select-Object -First 1
                if (-not $webDeployment) {
                    $webDeployment = $deployments.deployments | Where-Object {
                        $_.deploymentId -match "^AKfycb" -and $_.deploymentId -ne "@HEAD"
                    } | Select-Object -First 1
                }
                if ($webDeployment -and $webDeployment.deploymentId) {
                    $deploymentId = $webDeployment.deploymentId
                }
            } catch {
                # ignore parse errors
            }
        }
    }

    $deployUrl = $null

    $claspJson = Get-Content ".clasp.json" -Raw | ConvertFrom-Json
    $scriptId = $claspJson.scriptId
    if ($deploymentId) {
        $deployUrl = "https://script.google.com/macros/s/$deploymentId/exec"
    } elseif ($scriptId) {
        $deployUrl = "https://script.google.com/macros/s/$scriptId/exec"
    } else {
        throw "Could not resolve deploy URL. Run: clasp deployments"
    }

    $info = [ordered]@{
        uploadSecret = $Secret
        deployUrl    = $deployUrl
        scriptId     = $scriptId
        deploymentId = $deploymentId
        deployedAt   = (Get-Date).ToString("o")
    }
    $info | ConvertTo-Json | Set-Content -Path $DeployInfoFile -Encoding UTF8

    Write-Host ""
    Write-Host "URL:    $deployUrl" -ForegroundColor Green
    Write-Host "Secret: $Secret" -ForegroundColor Green

    if (-not $SkipUnityUpdate) {
        if (-not (Test-Path $UnityScene)) {
            Write-Warning "Unity scene not found: $UnityScene"
        } else {
            $scene = Get-Content $UnityScene -Raw
            $scene = $scene -replace "csvDriveUploadUrl:.*", "csvDriveUploadUrl: $deployUrl"
            $scene = $scene -replace "csvDriveUploadSecret:.*", "csvDriveUploadSecret: $Secret"
            Set-Content -Path $UnityScene -Value $scene -Encoding UTF8
            Write-Host "Updated SampleScene.unity" -ForegroundColor Green
        }
    }
}
finally {
    Pop-Location
}

Write-Host "Done."
