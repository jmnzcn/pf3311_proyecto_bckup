@echo off
setlocal
set UNITY="C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe"
set PROJECT="%~dp0.."
set LOG="%~dp0..\Logs\standalone-build.log"

if not exist %UNITY% (
  echo Unity 6000.3.11f1 no encontrado en %UNITY%
  exit /b 1
)

echo Cerrá el Editor de Unity con este proyecto antes de continuar.
echo Log: %LOG%
echo.

%UNITY% -batchmode -nographics -quit -projectPath %PROJECT% -executeMethod StandaloneBuild.PerformWindowsBuild -logFile %LOG%
set ERR=%ERRORLEVEL%

if %ERR% neq 0 (
  echo Build fallo. Revisar %LOG%
  exit /b %ERR%
)

echo Build OK: Build\Windows\ExperimentPrototypeB03230.exe
exit /b 0
