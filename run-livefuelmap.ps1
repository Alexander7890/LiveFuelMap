param(
    [int]$Port = 5000,
    [switch]$SkipMigration,
    [switch]$SkipFrontendBuild
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

Set-Location $root
. (Join-Path $root "scripts\NpmTools.ps1")

$env:DOTNET_CLI_HOME = $root
$env:APPDATA = Join-Path $root ".appdata"
$env:LOCALAPPDATA = Join-Path $root ".localappdata"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

if (-not (Test-Path (Join-Path $root ".env"))) {
    Copy-Item -Path (Join-Path $root ".env.example") -Destination (Join-Path $root ".env")
    Write-Host "Created .env from .env.example. Edit DB_PASSWORD/Jwt__Secret if needed, then run this script again." -ForegroundColor Yellow
    exit 1
}

if (-not $SkipFrontendBuild) {
    $frontend = Join-Path $root "frontend"
    Push-Location $frontend
    try {
        $npm = Get-NpmCli
        if (-not (Test-Path (Join-Path $frontend "node_modules"))) {
            Write-Host "Installing frontend dependencies..." -ForegroundColor Cyan
            $installArgs = @($npm.Args) + @("ci")
            & $npm.Command @installArgs
        }

        Write-Host "Building React frontend..." -ForegroundColor Cyan
        $buildArgs = @($npm.Args) + @("run", "build")
        & $npm.Command @buildArgs
    }
    finally {
        Pop-Location
    }
}

if (-not $SkipMigration) {
    Write-Host "Applying EF Core migrations..." -ForegroundColor Cyan
    dotnet ef database update `
        --project "src\LiveFuelMap.DAL\LiveFuelMap.DAL.csproj" `
        --startup-project "src\LiveFuelMap.Api\LiveFuelMap.Api.csproj"
}

$url = "http://localhost:$Port"
Write-Host "Starting LiveFuelMap API and built frontend at $url" -ForegroundColor Cyan
Write-Host "Swagger: $url/swagger" -ForegroundColor Cyan
Write-Host "Press Ctrl+C to stop." -ForegroundColor Cyan

dotnet run `
    --project "src\LiveFuelMap.Api\LiveFuelMap.Api.csproj" `
    --no-launch-profile `
    --urls $url
