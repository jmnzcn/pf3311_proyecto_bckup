<powershell>
# PF-3311 — first-boot setup on Windows Server (NICE DCV + experiment folder)
$ErrorActionPreference = "Stop"

# High performance power plan
powercfg /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c

New-Item -ItemType Directory -Force -Path "C:\Experimento\CSV data" | Out-Null

$base = "https://d1uj6qtbmh3dt5.cloudfront.net/2024.0/Servers"
$dcv = "$env:TEMP\NiceDcvServer.msi"
$web = "$env:TEMP\NiceDcvWebViewer.msi"

Invoke-WebRequest -Uri "$base/nice-dcv-server-x64-Release-2024.0-0-0.msi" -OutFile $dcv
Start-Process msiexec.exe -Wait -ArgumentList "/i `"$dcv`" /quiet /norestart"
Invoke-WebRequest -Uri "$base/nice-dcv-web-viewer-x64-Release-2024.0-0-0.msi" -OutFile $web
Start-Process msiexec.exe -Wait -ArgumentList "/i `"$web`" /quiet /norestart"

$dcvConf = "C:\Program Files\NICE\dcv\server\conf\dcv.conf"
if (Test-Path $dcvConf) {
    Add-Content -Path $dcvConf -Value @"

[display]
max-head-resolution=(1920, 1080)

[connectivity]
enable-quic-frontend=true
quic-port=8443
"@
}

# Log for debugging first boot
"PF3311 user-data finished at $(Get-Date -Format o)" | Out-File "C:\Experimento\setup-log.txt" -Encoding utf8
</powershell>
