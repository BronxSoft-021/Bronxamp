@echo off
rem Precica "BronxSoft Bronxamp" na desktopu -> publish\Bronxamp.exe
cd /d "%~dp0"
if not exist "publish\Bronxamp.exe" (
  echo Prvo pokreni "Napravi publish.bat".
  pause
  exit /b 1
)
powershell -NoProfile -Command "$d=[Environment]::GetFolderPath('Desktop'); $e=(Resolve-Path 'publish\Bronxamp.exe').Path; foreach ($n in 'UnderGRAND Plejer.lnk','BronxSoft Bronxamp.lnk') { $o=Join-Path $d $n; if (Test-Path $o) { Remove-Item $o } }; $s=(New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $d 'BronxSoft Bronxamp.lnk')); $s.TargetPath=$e; $s.WorkingDirectory=(Split-Path $e); $s.IconLocation=$e + ',0'; $s.Description='BronxSoft Bronxamp'; $s.Save(); 'Precica napravljena: ' + (Join-Path $d 'BronxSoft Bronxamp.lnk')"
