# One-time authorization for the PF3311 CSV web app (Google account: neyfred@gmail.com).
# Run from ANY folder, e.g.:
#   powershell -File "C:\Users\neyfr\Downloads\ExperimentPrototypeB03230\_tools\google_drive\authorize_webapp.ps1"
$scriptId = "1Wxnk2wWQ4qMAhRJ_VoNLKkbfzCX4PTkML__jYBGBwK_8VuwBrml1B5dj"
$deployId = "AKfycbyA-DVdtaTdWgRXqjgrusL2m5cVJW7x3kZiJGRIZLEt6qCdsIcqAoc1NJyQkkcg-Iuw"
$secret = (Get-Content "$PSScriptRoot\upload_secret.txt" -Raw).Trim()

Write-Host "1. In the script editor: select AUTORIZAR_PERMISOS_UNA_VEZ -> Run -> approve permissions." -ForegroundColor Yellow
Start-Process "https://script.google.com/home/projects/$scriptId/edit"

Start-Sleep -Seconds 2

Write-Host "2. In the browser tab: approve web app access if asked." -ForegroundColor Yellow
Start-Process "https://script.google.com/macros/s/$deployId/exec?ping=1"

Start-Sleep -Seconds 3

Write-Host "3. Testing upload endpoint..."
$body = @{
  secret = $secret
  sessionFolderName = "AUTH_TEST"
  files = @(@{
    name = "ping.csv"
    contentBase64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("ok,test`n"))
  })
} | ConvertTo-Json -Compress
$url = "https://script.google.com/macros/s/$deployId/exec"
try {
  $r = Invoke-RestMethod -Uri $url -Method POST -Body $body -ContentType "application/json"
  if ($r.ok) {
    Write-Host "SUCCESS: Drive upload works. fileCount=$($r.fileCount)" -ForegroundColor Green
  } else {
    Write-Host "Response: $($r | ConvertTo-Json -Compress)" -ForegroundColor Red
  }
} catch {
  Write-Host "Still failing: $($_.Exception.Message)" -ForegroundColor Red
  Write-Host "Complete AUTORIZAR_PERMISOS_UNA_VEZ in the editor (see AUTORIZAR_UNA_VEZ.txt), then run this script again." -ForegroundColor Yellow
}
