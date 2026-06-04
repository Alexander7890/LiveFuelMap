param(
    [int]$Port = 5173
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$frontend = Join-Path $root "frontend"
. (Join-Path $root "scripts\NpmTools.ps1")

function Read-DotEnv {
    param([string]$Path)

    $values = @{}
    if (-not (Test-Path -LiteralPath $Path)) {
        return $values
    }

    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmed = $line.Trim()
        if ($trimmed.Length -eq 0 -or $trimmed.StartsWith("#")) {
            continue
        }

        $separator = $trimmed.IndexOf("=")
        if ($separator -le 0) {
            continue
        }

        $name = $trimmed.Substring(0, $separator).Trim()
        $value = $trimmed.Substring($separator + 1).Trim()
        if ($value.Length -ge 2 -and
            (($value.StartsWith('"') -and $value.EndsWith('"')) -or
             ($value.StartsWith("'") -and $value.EndsWith("'")))) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        $values[$name] = $value
    }

    return $values
}

$rootEnv = Read-DotEnv (Join-Path $root ".env")

function Set-EnvIfMissing {
    param(
        [string]$Name,
        [string[]]$FallbackKeys
    )

    if ([Environment]::GetEnvironmentVariable($Name, "Process")) {
        return
    }

    foreach ($key in $FallbackKeys) {
        if ($rootEnv.ContainsKey($key) -and -not [string]::IsNullOrWhiteSpace($rootEnv[$key])) {
            [Environment]::SetEnvironmentVariable($Name, $rootEnv[$key], "Process")
            return
        }
    }
}

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
    Set-EnvIfMissing -Name "VITE_CAPTCHA_ENABLED" -FallbackKeys @("VITE_CAPTCHA_ENABLED", "CAPTCHA_ENABLED", "Captcha__Enabled")
    Set-EnvIfMissing -Name "VITE_CAPTCHA_PROVIDER" -FallbackKeys @("VITE_CAPTCHA_PROVIDER", "CAPTCHA_PROVIDER", "Captcha__Provider")
    Set-EnvIfMissing -Name "VITE_CAPTCHA_SITE_KEY" -FallbackKeys @("VITE_CAPTCHA_SITE_KEY", "CAPTCHA_SITE_KEY", "Captcha__SiteKey")

    Write-Host "Starting React + Vite frontend at http://localhost:$Port" -ForegroundColor Cyan
    Write-Host "Backend API should run separately: .\run-backend.cmd" -ForegroundColor Cyan
    $devArgs = @($npm.Args) + @("run", "dev", "--", "--port", "$Port")
    & $npm.Command @devArgs
}
finally {
    Pop-Location
}
