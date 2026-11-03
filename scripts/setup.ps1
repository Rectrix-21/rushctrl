param([switch]$PortableDatabase)
. (Join-Path $PSScriptRoot 'local-common.ps1')
$rushRoot = Get-RushRoot
Push-Location $rushRoot
try {
    if (-not (Get-Command node -ErrorAction SilentlyContinue)) { throw 'Install Node.js 24 before running setup.' }
    $rushNode = & node --version
    if ($rushNode -notmatch '^v24\.') { throw 'This project uses Node.js 24.' }
    New-Item -ItemType Directory -Path '.tools/downloads', '.tools/dotnet', '.local' -Force | Out-Null
    & node scripts/create-env.mjs
    Assert-RushSuccess 'Local configuration generation'
    $rushConfig = Get-RushConfiguration
    if (-not (Test-Path -LiteralPath '.tools/dotnet/dotnet.exe')) {
        Write-Output 'Downloading the project-local .NET SDK from Microsoft.'
        $rushZip = '.tools/downloads/dotnet-sdk-10.0.403.zip'
        Invoke-WebRequest 'https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.403/dotnet-sdk-10.0.403-win-x64.zip' -OutFile $rushZip
        $rushExpected = '7a114c318fa7f1d9822991b534b2814d0e78386ceda816b371f5141acefe741d87ccb606ede06cadac8f9ac5070e32b708a67bbd1619ff5e5a7858a27874ef48'
        if ((Get-FileHash -LiteralPath $rushZip -Algorithm SHA512).Hash -ne $rushExpected) { throw 'SDK checksum mismatch.' }
        Expand-Archive -LiteralPath $rushZip -DestinationPath '.tools/dotnet' -Force
    }
    Set-RushEnvironment
    & '.tools/dotnet/dotnet.exe' --version
    Assert-RushSuccess 'SDK verification'
    & '.tools/dotnet/dotnet.exe' tool restore
    Assert-RushSuccess 'EF tool restore'
    & '.tools/dotnet/dotnet.exe' restore RushCtrl.slnx
    Assert-RushSuccess 'API restore'
    & npm.cmd ci --no-fund --no-audit
    Assert-RushSuccess 'Frontend restore'
    if ($PortableDatabase) {
        if (-not (Test-Path -LiteralPath '.tools/postgres/pgsql/bin/postgres.exe')) {
            Write-Output 'Downloading portable PostgreSQL 18.6 from EDB; no Windows service is installed.'
            Invoke-WebRequest 'https://sbp.enterprisedb.com/getfile.jsp?fileid=1260609' -OutFile '.tools/downloads/postgresql-18.6-windows-x64.zip'
            Expand-Archive -LiteralPath '.tools/downloads/postgresql-18.6-windows-x64.zip' -DestinationPath '.tools/postgres' -Force
        }
        & (Join-Path $PSScriptRoot 'database.ps1') start
    }
    Write-Output 'Setup complete. Use ./scripts/dev.ps1 for portable local development, or docker compose up --build for the container stack.'
} finally { Pop-Location }
