@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-DocSuite.ps1"
if errorlevel 1 (
  echo.
  echo Installation failed. Read the error above.
  pause
)
