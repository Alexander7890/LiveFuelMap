@echo off
setlocal
cd /d "%~dp0"

echo Starting LiveFuelMap backend...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-backend.ps1" %*

set EXIT_CODE=%ERRORLEVEL%
echo.
echo Backend process finished with exit code %EXIT_CODE%.
echo If the backend closed unexpectedly, copy the error text above.
echo.
pause
exit /b %EXIT_CODE%
