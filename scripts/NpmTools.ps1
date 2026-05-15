function Get-NodeCommand {
    $node = Get-Command node -ErrorAction SilentlyContinue
    if ($node) {
        return $node.Source
    }

    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "OpenAI\Codex\bin\node.exe"),
        (Join-Path $env:USERPROFILE ".cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe")
    )

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path $candidate)) {
            return $candidate
        }
    }

    throw "Node.js is required for frontend. Install Node.js LTS from https://nodejs.org/ or add node.exe to PATH."
}

function Get-NpmCli {
    $npmCmd = Get-Command npm.cmd -ErrorAction SilentlyContinue
    if ($npmCmd) {
        return @{ Command = $npmCmd.Source; Args = @() }
    }

    $npm = Get-Command npm -ErrorAction SilentlyContinue
    if ($npm -and [System.IO.Path]::GetExtension($npm.Source) -ne ".ps1") {
        return @{ Command = $npm.Source; Args = @() }
    }

    $nodeCommand = Get-NodeCommand
    $root = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
    $npmCli = Join-Path $root ".tools\npm\package\bin\npm-cli.js"
    if (-not (Test-Path $npmCli)) {
        $tools = Join-Path $root ".tools"
        $npmDir = Join-Path $tools "npm"
        $tgz = Join-Path $tools "npm.tgz"
        New-Item -ItemType Directory -Force -Path $tools | Out-Null
        Write-Host "npm was not found in PATH. Downloading local npm CLI..." -ForegroundColor Yellow
        Invoke-WebRequest -Uri "https://registry.npmjs.org/npm/-/npm-10.9.2.tgz" -OutFile $tgz
        if (Test-Path $npmDir) { Remove-Item -LiteralPath $npmDir -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $npmDir | Out-Null
        tar -xzf $tgz -C $npmDir
    }

    return @{ Command = $nodeCommand; Args = @($npmCli) }
}
