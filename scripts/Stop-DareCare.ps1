$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$pidFile = Join-Path $root '.localdata\server.pid'
if (-not (Test-Path $pidFile)) { Write-Host 'No tracked DareCare process found.'; exit 0 }
$tracked = @{}
foreach ($line in (Get-Content $pidFile)) {
    if ($line -match '^([^=]+)=(.*)$') { $tracked[$matches[1]] = $matches[2] }
}
if (-not $tracked.ContainsKey('Id') -or -not $tracked.ContainsKey('StartUtcTicks')) {
    Write-Host 'Invalid PID tracking file. No process was killed.'
    exit 1
}
$processId = [int]$tracked.Id
$process = Get-Process -Id $processId -ErrorAction SilentlyContinue
if ($null -ne $process -and $process.ProcessName -eq 'dotnet' -and
    $process.StartTime.ToUniversalTime().Ticks -eq [long]$tracked.StartUtcTicks) {
    & taskkill /PID $processId /T /F | Out-Host
} else {
    Write-Host 'Tracked process is gone or does not match. No unrelated process was killed.'
}
Remove-Item $pidFile -Force -ErrorAction SilentlyContinue
Write-Host 'Done.'
