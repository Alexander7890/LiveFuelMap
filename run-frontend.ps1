param(
    [int]$Port = 5173
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$frontend = Join-Path $root "frontend"
. (Join-Path $root "scripts\NpmTools.ps1")

Push-Location $frontend
try {
    $npm = Get-NpmCli

    if (-not (Test-Path (Join-Path $frontend "node_modules"))) {
        Write-Host "Installing frontend dependencies..." -ForegroundColor Cyan
        $installArgs = @($npm.Args) + @("install")
        & $npm.Command @installArgs
    }

    if (-not $env:VITE_API_BASE_URL) {
        $env:VITE_API_BASE_URL = "http://localhost:5000"
    }
    if (-not $env:VITE_SIGNALR_HUB_URL) {
        $env:VITE_SIGNALR_HUB_URL = "http://localhost:5000/hubs/fuel"
    }

    Write-Host "Starting React + Vite frontend at http://localhost:$Port" -ForegroundColor Cyan
    Write-Host "Backend API should run separately: .\run-backend.cmd" -ForegroundColor Cyan
    $devArgs = @($npm.Args) + @("run", "dev", "--", "--port", "$Port")
    & $npm.Command @devArgs
}
finally {
    Pop-Location
}
