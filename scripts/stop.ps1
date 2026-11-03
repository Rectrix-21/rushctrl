. (Join-Path $PSScriptRoot 'local-common.ps1')
$rushRoot = Get-RushRoot
$rushRecord = Join-Path $rushRoot '.local/app-processes.json'
if (-not (Test-Path -LiteralPath $rushRecord)) { Write-Output 'No local app processes are recorded.'; return }
$rushRecorded = Get-Content -LiteralPath $rushRecord -Raw | ConvertFrom-Json
$rushProcesses = Get-CimInstance Win32_Process
function Stop-RushProcessTree([int]$ProcessId) {
    foreach ($rushChild in $rushProcesses | Where-Object ParentProcessId -eq $ProcessId) { Stop-RushProcessTree $rushChild.ProcessId }
    Stop-Process -Id $ProcessId -ErrorAction SilentlyContinue
}
foreach ($rushId in @($rushRecorded.api, $rushRecorded.web)) {
    $rushProcess = $rushProcesses | Where-Object ProcessId -eq $rushId
    if ($rushProcess -and $rushProcess.CommandLine -like "*$rushRoot*" -and
        ($rushProcess.CommandLine -match 'RushCtrl\.Api\.dll|next[/\\]dist[/\\]bin[/\\]next')) { Stop-RushProcessTree $rushId }
}
Remove-Item -LiteralPath $rushRecord
Write-Output 'Recorded app processes stopped. The local database remains running; stop it with ./scripts/database.ps1 stop.'
