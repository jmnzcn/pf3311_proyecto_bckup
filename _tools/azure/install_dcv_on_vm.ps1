# PF-3311 — Install NICE DCV on Azure Windows VM (GPU or CPU)
$ErrorActionPreference = "Continue"
New-Item -ItemType Directory -Force -Path "C:\Experimento\CSV data" | Out-Null

if (-not (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*" -ErrorAction SilentlyContinue |
        Where-Object { $_.DisplayName -like "Microsoft Visual C++ 2022*" })) {
    Invoke-WebRequest -Uri "https://aka.ms/vs/17/release/vc_redist.x64.exe" -OutFile "$env:TEMP\vc_redist.x64.exe"
    Start-Process "$env:TEMP\vc_redist.x64.exe" -ArgumentList "/install /passive /norestart" -Wait
}

Invoke-WebRequest -Uri "https://d1uj6qtbmh3dt5.cloudfront.net/nice-dcv-virtual-display-x64-Release.msi" -OutFile "$env:TEMP\DCVDisplayDriver.msi"
Invoke-WebRequest -Uri "https://d1uj6qtbmh3dt5.cloudfront.net/nice-dcv-server-x64-Release.msi" -OutFile "$env:TEMP\DCVServer.msi"

Start-Process msiexec.exe -ArgumentList "/I `"$env:TEMP\DCVDisplayDriver.msi`" /quiet /norestart" -Wait
Start-Process msiexec.exe -ArgumentList "/I `"$env:TEMP\DCVServer.msi`" ADDLOCAL=ALL /quiet /norestart /l*v C:\Windows\Temp\dcv_install.log" -Wait

reg add "HKLM\SOFTWARE\GSettings\com\nicesoftware\dcv\session-management" /v create-session /t REG_DWORD /d 1 /f
reg add "HKLM\SOFTWARE\GSettings\com\nicesoftware\dcv\session-management\automatic-console-session" /v owner /t REG_SZ /d Administrator /f
reg add "HKLM\SOFTWARE\GSettings\com\nicesoftware\dcv\connectivity" /v enable-quic-frontend /t REG_DWORD /d 1 /f
reg add "HKLM\SOFTWARE\GSettings\com\nicesoftware\dcv\display" /v max-head-resolution /t REG_SZ /d "(1280,720)" /f

New-NetFirewallRule -DisplayName "NICE DCV 8443 TCP" -Direction Inbound -Protocol TCP -LocalPort 8443 -Action Allow -ErrorAction SilentlyContinue
New-NetFirewallRule -DisplayName "NICE DCV 8443 UDP" -Direction Inbound -Protocol UDP -LocalPort 8443 -Action Allow -ErrorAction SilentlyContinue
Set-Service Audiosrv -StartupType Automatic -ErrorAction SilentlyContinue
Start-Service Audiosrv -ErrorAction SilentlyContinue
Restart-Service dcvserver -ErrorAction SilentlyContinue

Get-Service dcvserver | Format-List Name, Status, StartType
Write-Output "DCV install done"
