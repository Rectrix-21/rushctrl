$ErrorActionPreference = 'Stop'

function Get-RushRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}

function Get-RushConfiguration {
    $rushRoot = Get-RushRoot
    $rushEnvPath = Join-Path $rushRoot '.env'
    if (-not (Test-Path -LiteralPath $rushEnvPath)) {
        throw 'Missing .env. Run ./scripts/setup.ps1 first.'
    }
    $rushConfig = @{}
    foreach ($rushLine in Get-Content -LiteralPath $rushEnvPath) {
        if ($rushLine -match '^([A-Z_]+)=(.*)$') { $rushConfig[$Matches[1]] = $Matches[2] }
    }
    foreach ($rushKey in @('POSTGRES_USER', 'POSTGRES_DB', 'POSTGRES_PASSWORD', 'POSTGRES_PORT', 'API_PORT', 'WEB_PORT')) {
        if ([string]::IsNullOrWhiteSpace($rushConfig[$rushKey])) { throw "Missing $rushKey in .env. Run setup.ps1 or complete the configuration." }
    }
    foreach ($rushKey in @('POSTGRES_USER', 'POSTGRES_DB')) {
        if ($rushConfig[$rushKey] -notmatch '^[a-z][a-z0-9_]*$') { throw "$rushKey must use lowercase letters, digits and underscores." }
    }
    foreach ($rushKey in @('POSTGRES_PORT', 'API_PORT', 'WEB_PORT')) {
        $rushPort = 0
        if (-not [int]::TryParse($rushConfig[$rushKey], [ref]$rushPort) -or $rushPort -lt 1024 -or $rushPort -gt 65535) {
            throw "$rushKey must be a port between 1024 and 65535."
        }
    }
    if (($rushConfig['POSTGRES_PORT'], $rushConfig['API_PORT'], $rushConfig['WEB_PORT'] | Select-Object -Unique).Count -ne 3) {
        throw 'Database, API and web ports must be different.'
    }
    return $rushConfig
}

function Set-RushEnvironment {
    $rushRoot = Get-RushRoot
    $env:DOTNET_ROOT = Join-Path $rushRoot '.tools/dotnet'
    $env:PATH = $env:DOTNET_ROOT + [IO.Path]::PathSeparator + $env:PATH
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:NEXT_TELEMETRY_DISABLED = '1'
    $env:NODE_USE_SYSTEM_CA = '1'
}

function Get-RushConnectionString($Configuration, [string]$Database = '') {
    if (-not $Database) { $Database = $Configuration['POSTGRES_DB'] }
    $rushPassword = $Configuration['POSTGRES_PASSWORD'].Replace('"', '""')
    return ('Host=127.0.0.1;Port={0};Database={1};Username={2};Password="{3}";Timeout=3' -f
        $Configuration['POSTGRES_PORT'], $Database, $Configuration['POSTGRES_USER'], $rushPassword)
}

function Assert-RushSuccess([string]$Operation) {
    if ($LASTEXITCODE -ne 0) { throw "$Operation failed with exit code $LASTEXITCODE." }
}
