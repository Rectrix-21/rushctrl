param([switch]$WithoutDatabase)
. (Join-Path $PSScriptRoot 'local-common.ps1')
$rushRoot = Get-RushRoot
Set-RushEnvironment
Push-Location $rushRoot
try {
    $rushTestArgs = @('test', 'RushCtrl.slnx', '--configuration', 'Release', '--nologo', '--logger', 'console;verbosity=minimal')
    if ($WithoutDatabase) { $rushTestArgs += @('--filter', 'Category!=Database') }
    else {
        $rushConfig = Get-RushConfiguration
        $env:RUSHCTRL_TEST_DATABASE_CONNECTION = Get-RushConnectionString $rushConfig 'postgres'
    }
    & '.tools/dotnet/dotnet.exe' @rushTestArgs
    Assert-RushSuccess 'API tests'
    foreach ($rushTask in @('test', 'typecheck', 'lint', 'build')) {
        & npm.cmd run $rushTask
        Assert-RushSuccess "Frontend $rushTask"
    }
} finally { Remove-Item Env:RUSHCTRL_TEST_DATABASE_CONNECTION -ErrorAction SilentlyContinue; Pop-Location }
