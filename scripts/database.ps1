param([ValidateSet('start', 'stop', 'status')][string]$Action = 'start')
. (Join-Path $PSScriptRoot 'local-common.ps1')
$rushRoot = Get-RushRoot
$rushConfig = Get-RushConfiguration
Push-Location $rushRoot
try {
    $rushBin = Join-Path $rushRoot '.tools/postgres/pgsql/bin'
    $rushData = Join-Path $rushRoot '.local/pgdata'
    if (-not (Test-Path -LiteralPath (Join-Path $rushBin 'pg_ctl.exe'))) { throw 'Portable PostgreSQL is missing. Run ./scripts/setup.ps1 -PortableDatabase.' }
    if ($Action -eq 'status') {
        & (Join-Path $rushBin 'pg_ctl.exe') -D $rushData status
        exit $LASTEXITCODE
    }
    if ($Action -eq 'stop') {
        & (Join-Path $rushBin 'pg_ctl.exe') -D $rushData -m fast -w stop
        Assert-RushSuccess 'Database stop'
        return
    }
    if (-not (Test-Path -LiteralPath (Join-Path $rushData 'PG_VERSION'))) {
        New-Item -ItemType Directory -Path '.local' -Force | Out-Null
        $rushPasswordPath = Join-Path $rushRoot '.local/pg-password'
        [IO.File]::WriteAllText($rushPasswordPath, $rushConfig['POSTGRES_PASSWORD'])
        & (Join-Path $rushBin 'initdb.exe') -D $rushData -U $rushConfig['POSTGRES_USER'] -A scram-sha-256 "--pwfile=$rushPasswordPath" --encoding=UTF8 --locale=C
        Assert-RushSuccess 'Database initialization'
    }
    & (Join-Path $rushBin 'pg_ctl.exe') -D $rushData status *> $null
    if ($LASTEXITCODE -ne 0) {
        & (Join-Path $rushBin 'pg_ctl.exe') -D $rushData -l (Join-Path $rushRoot '.local/postgres.log') -o "-h 127.0.0.1 -p $($rushConfig['POSTGRES_PORT'])" -w start
        Assert-RushSuccess 'Database start'
    }
    $env:PGPASSWORD = $rushConfig['POSTGRES_PASSWORD']
    $rushExists = & (Join-Path $rushBin 'psql.exe') -h 127.0.0.1 -p $rushConfig['POSTGRES_PORT'] -U $rushConfig['POSTGRES_USER'] -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='$($rushConfig['POSTGRES_DB'])'"
    Assert-RushSuccess 'Database lookup'
    if ($rushExists -ne '1') {
        & (Join-Path $rushBin 'createdb.exe') -h 127.0.0.1 -p $rushConfig['POSTGRES_PORT'] -U $rushConfig['POSTGRES_USER'] $rushConfig['POSTGRES_DB']
        Assert-RushSuccess 'Application database creation'
    }
    Write-Output "Portable PostgreSQL is ready on 127.0.0.1:$($rushConfig['POSTGRES_PORT'])."
} finally { Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue; Pop-Location }
