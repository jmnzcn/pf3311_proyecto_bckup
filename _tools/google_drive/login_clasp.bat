@echo off
echo.
echo === Login Google (clasp) para subir CSV a Drive ===
echo.
echo Se va a abrir el navegador. Inicia sesion con la cuenta duena de tu Drive.
echo Si no abre solo, copia el link que aparezca abajo y pegalo en Chrome.
echo.
cd /d "%~dp0clasp_project"
clasp login
if errorlevel 1 (
  echo.
  echo Si fallo, probá: clasp login --no-localhost
  pause
  exit /b 1
)
echo.
echo Login OK. Ahora corré deploy_drive_uploader.ps1
pause
