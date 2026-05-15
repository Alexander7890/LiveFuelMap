@echo off
setlocal
cd /d "%~dp0"

echo Starting LiveFuelMap frontend...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-frontend.ps1" %*

set EXIT_CODE=%ERRORLEVEL%
echo.
echo Frontend process finished with exit code %EXIT_CODE%.
echo If the frontend closed unexpectedly, copy the error text above.
echo.
pause
exit /b %EXIT_CODE%
