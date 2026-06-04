param(
    [int]$Port = 5000,
    [switch]$SkipMigration,
    [switch]$StopExisting
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$env:DOTNET_CLI_HOME = $root
$env:APPDATA = Join-Path $root ".appdata"
$env:LOCALAPPDATA = Join-Path $root ".localappdata"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

function Get-PortOwners {
    param([int]$TargetPort)

    $connections = Get-NetTCPConnection -LocalPort $TargetPort -State Listen -ErrorAction SilentlyContinue
    if (-not $connections) {
        return @()
    }

    $ownerIds = $connections | Select-Object -ExpandProperty OwningProcess -Unique
    return @($ownerIds | ForEach-Object {
        $process = Get-Process -Id $_ -ErrorAction SilentlyContinue
        if ($process) {
            [PSCustomObject]@{
                Id = $process.Id
                Name = $process.ProcessName
                Path = $process.Path
            }
        }
    })
}

if (-not (Test-Path (Join-Path $root ".env"))) {
    Copy-Item -Path (Join-Path $root ".env.example") -Destination (Join-Path $root ".env")
    Write-Host "Created .env from .env.example. Edit DB_PASSWORD/Jwt__Secret if needed, then run this script again." -ForegroundColor Yellow
    exit 1
}

$portOwners = @(Get-PortOwners -TargetPort $Port)
if ($portOwners.Count -gt 0) {
    if ($StopExisting) {
        foreach ($owner in $portOwners) {
            Write-Host "Stopping process on port ${Port}: $($owner.Name) (PID $($owner.Id))" -ForegroundColor Yellow
            Stop-Process -Id $owner.Id -Force -ErrorAction SilentlyContinue
        }
        Start-Sleep -Seconds 1
        $remainingOwners = @(Get-PortOwners -TargetPort $Port)
        if ($remainingOwners.Count -gt 0) {
            Write-Host "Port $Port is still busy after stopping existing process." -ForegroundColor Red
            foreach ($owner in $remainingOwners) {
                Write-Host "PID $($owner.Id): $($owner.Name) $($owner.Path)" -ForegroundColor Red
            }
            exit 1
        }
    }
    else {
        Write-Host "Port $Port is already in use. Backend is probably already running." -ForegroundColor Yellow
        foreach ($owner in $portOwners) {
            Write-Host "PID $($owner.Id): $($owner.Name) $($owner.Path)" -ForegroundColor Yellow
        }
        Write-Host "Open http://localhost:$Port/swagger if you only need the running backend." -ForegroundColor Cyan
        Write-Host "To restart it, run: .\run-backend.ps1 -StopExisting" -ForegroundColor Cyan
        exit 1
    }
}

if (-not $SkipMigration) {
    Write-Host "Applying EF Core migrations..." -ForegroundColor Cyan
    dotnet ef database update `
        --project "src\LiveFuelMap.DAL\LiveFuelMap.DAL.csproj" `
        --startup-project "src\LiveFuelMap.Api\LiveFuelMap.Api.csproj"
}

$url = "http://localhost:$Port"
Write-Host "Starting LiveFuelMap backend API at $url" -ForegroundColor Cyan
Write-Host "Swagger: $url/swagger" -ForegroundColor Cyan
Write-Host "React frontend dev server should run separately: .\run-frontend.ps1" -ForegroundColor Cyan
Write-Host "Press Ctrl+C to stop." -ForegroundColor Cyan

dotnet run `
    --project "src\LiveFuelMap.Api\LiveFuelMap.Api.csproj" `
    --no-launch-profile `
    --urls $url
