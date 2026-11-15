@echo off
rem Pravi publish\Bronxamp.exe (pre toga zatvori plejer)
cd /d "%~dp0"
set DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet publish Bronxamp.csproj -c Release -r win-x64 --self-contained -nologo -v q -o publish
if errorlevel 1 (
  echo.
  echo Pakovanje nije uspelo.
  pause
  exit /b 1
)
echo.
echo Gotovo: publish\Bronxamp.exe
