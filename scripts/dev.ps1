param([switch]$SkipBuild)
. (Join-Path $PSScriptRoot 'local-common.ps1')
$rushRoot = Get-RushRoot
$rushConfig = Get-RushConfiguration
Set-RushEnvironment
Push-Location $rushRoot
try {
    if (Test-Path -LiteralPath '.local/app-processes.json') { throw 'A previous local launch is recorded. Run ./scripts/stop.ps1 before starting again.' }
    & (Join-Path $PSScriptRoot 'database.ps1') start
    $env:ConnectionStrings__Database = Get-RushConnectionString $rushConfig
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = "http://127.0.0.1:$($rushConfig['API_PORT'])"
    $env:API_BASE_URL = $env:ASPNETCORE_URLS
    if (-not $SkipBuild) {
        & '.tools/dotnet/dotnet.exe' build src/RushCtrl.Api --nologo
        Assert-RushSuccess 'API build'
    }
    $rushDll = Join-Path $rushRoot 'src/RushCtrl.Api/bin/Debug/net10.0/RushCtrl.Api.dll'
    & '.tools/dotnet/dotnet.exe' $rushDll --migrate
    Assert-RushSuccess 'Migration application'
    $rushApi = Start-Process -FilePath (Join-Path $rushRoot '.tools/dotnet/dotnet.exe') -ArgumentList ('"' + $rushDll + '"') -WorkingDirectory $rushRoot -WindowStyle Hidden -RedirectStandardOutput '.local/api.log' -RedirectStandardError '.local/api-errors.log' -PassThru
    $rushNext = Join-Path $rushRoot 'node_modules/next/dist/bin/next'
    $rushWebArgs = '"' + $rushNext + '" dev "' + (Join-Path $rushRoot 'apps/web') + '" --hostname 127.0.0.1 --port ' + $rushConfig['WEB_PORT']
    $rushWeb = Start-Process -FilePath (Get-Command node).Source -ArgumentList $rushWebArgs -WorkingDirectory $rushRoot -WindowStyle Hidden -RedirectStandardOutput '.local/web.log' -RedirectStandardError '.local/web-errors.log' -PassThru
    [IO.File]::WriteAllText((Join-Path $rushRoot '.local/app-processes.json'), (@{ api = $rushApi.Id; web = $rushWeb.Id } | ConvertTo-Json))
    Write-Output "Web: http://localhost:$($rushConfig['WEB_PORT'])"
    Write-Output "API: http://localhost:$($rushConfig['API_PORT'])/health/ready"
    Write-Output 'Logs are in .local/. Stop app processes with ./scripts/stop.ps1.'
} finally { Remove-Item Env:ConnectionStrings__Database -ErrorAction SilentlyContinue; Pop-Location }
