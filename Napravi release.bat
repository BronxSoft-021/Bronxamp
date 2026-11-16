@echo off
rem Zipuje publish i kači ga na GitHub Releases (traži gh prijavljen i publish napravljen)
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "Napravi release.ps1"
if errorlevel 1 (
  echo.
  echo Release nije uspeo.
  pause
  exit /b 1
)
pause
