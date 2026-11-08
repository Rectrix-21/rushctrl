param([switch]$SkipBuild)
. (Join-Path $PSScriptRoot 'local-common.ps1')
$rushRoot = Get-RushRoot
& node (Join-Path $PSScriptRoot 'create-env.mjs')
Assert-RushSuccess 'Local credential setup'
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
    $env:Demo__Enabled = 'true'
    $env:Simulation__Enabled = 'true'
    $env:Demo__ManagerPassword = $rushConfig['DEMO_MANAGER_PASSWORD']
    $env:Demo__EmployeePassword = $rushConfig['DEMO_EMPLOYEE_PASSWORD']
    Write-Output 'Applying migrations and preparing synthetic accounts. Details: .local/migrate.log'
    & '.tools/dotnet/dotnet.exe' $rushDll --migrate --seed-demo *> '.local/migrate.log'
    Assert-RushSuccess 'Migration application'
    foreach ($rushVariable in @('Demo__Enabled', 'Demo__ManagerPassword', 'Demo__EmployeePassword')) {
        Remove-Item "Env:$rushVariable" -ErrorAction SilentlyContinue
    }
    $rushApi = Start-Process -FilePath (Join-Path $rushRoot '.tools/dotnet/dotnet.exe') -ArgumentList ('"' + $rushDll + '"') -WorkingDirectory $rushRoot -WindowStyle Hidden -RedirectStandardOutput '.local/api.log' -RedirectStandardError '.local/api-errors.log' -PassThru
    Remove-Item Env:ConnectionStrings__Database -ErrorAction SilentlyContinue
    Remove-Item Env:Simulation__Enabled -ErrorAction SilentlyContinue
    $rushNext = Join-Path $rushRoot 'node_modules/next/dist/bin/next'
    $rushWebArgs = '"' + $rushNext + '" dev "' + (Join-Path $rushRoot 'apps/web') + '" --hostname 127.0.0.1 --port ' + $rushConfig['WEB_PORT']
    $rushWeb = Start-Process -FilePath (Get-Command node).Source -ArgumentList $rushWebArgs -WorkingDirectory $rushRoot -WindowStyle Hidden -RedirectStandardOutput '.local/web.log' -RedirectStandardError '.local/web-errors.log' -PassThru
    [IO.File]::WriteAllText((Join-Path $rushRoot '.local/app-processes.json'), (@{ api = $rushApi.Id; web = $rushWeb.Id } | ConvertTo-Json))
    Write-Output "Web: http://localhost:$($rushConfig['WEB_PORT'])"
    Write-Output "API: http://localhost:$($rushConfig['API_PORT'])/health/ready"
    Write-Output 'Logs are in .local/. Stop app processes with ./scripts/stop.ps1.'
} finally {
    foreach ($rushVariable in @('ConnectionStrings__Database', 'Demo__Enabled', 'Demo__ManagerPassword', 'Demo__EmployeePassword', 'Simulation__Enabled')) {
        Remove-Item "Env:$rushVariable" -ErrorAction SilentlyContinue
    }
    Pop-Location
}
