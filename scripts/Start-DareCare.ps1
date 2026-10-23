[CmdletBinding()]
param(
    [switch]$SqlServer,
    [switch]$TestOnly
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root
$api = Join-Path $root 'src\DareCare.Api'
$infra = Join-Path $root 'src\DareCare.Infrastructure'
$solution = Join-Path $root 'DareCare.slnx'
$localDir = Join-Path $root '.localdata'
New-Item -ItemType Directory -Force -Path $localDir | Out-Null
$ownedProcess = $null
$pidFile = Join-Path $localDir 'server.pid'

function Info([string]$message) { Write-Host "[DareCare] $message" -ForegroundColor Cyan }
function Run-Dotnet([string[]]$Parameters) {
    & dotnet @Parameters
    if ($LASTEXITCODE -ne 0) { throw "dotnet command failed; action: $($Parameters[0])" }
}
function Get-Health {
    try {
        $r = Invoke-RestMethod -Method Get -Uri 'http://127.0.0.1:5208/health' -TimeoutSec 2
        if ($r.service -eq 'DareCare.Api') { return $r }
    } catch {}
    return $null
}
function Is-Ready {
    $health = Get-Health
    if ($null -eq $health) { return $false }
    $expected = if ($SqlServer) { 'SqlServer' } else { 'Sqlite' }
    return $health.databaseProvider -eq $expected
}

try {
    Info 'Checking .NET 10 SDK...'
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        if (Get-Command winget -ErrorAction SilentlyContinue) {
            Info 'Installing .NET 10 SDK with Windows Package Manager (system approval may be required)...'
            & winget install --id Microsoft.DotNet.SDK.10 --exact --accept-source-agreements --accept-package-agreements
            if ($LASTEXITCODE -ne 0) { throw 'Automatic .NET installation failed. Download .NET 10 SDK from https://dotnet.microsoft.com/download/dotnet/10.0' }
            $env:PATH = "$env:ProgramFiles\dotnet;" + [Environment]::GetEnvironmentVariable('PATH','Machine') + ';' + [Environment]::GetEnvironmentVariable('PATH','User')
        } else {
            Start-Process 'https://dotnet.microsoft.com/download/dotnet/10.0'
            throw 'Please install .NET 10 SDK once, then double-click START_DARECARE.cmd again.'
        }
    }
    $sdks = (& dotnet --list-sdks) -join "`n"
    if ($sdks -notmatch '(?m)^10\.') {
        if (Get-Command winget -ErrorAction SilentlyContinue) {
            Info 'Installing missing .NET 10 SDK...'
            & winget install --id Microsoft.DotNet.SDK.10 --exact --accept-source-agreements --accept-package-agreements
            $env:PATH = "$env:ProgramFiles\dotnet;" + [Environment]::GetEnvironmentVariable('PATH','Machine') + ';' + [Environment]::GetEnvironmentVariable('PATH','User')
            $sdks = (& dotnet --list-sdks) -join "`n"
        }
        if ($sdks -notmatch '(?m)^10\.') {
            Start-Process 'https://dotnet.microsoft.com/download/dotnet/10.0'
            throw 'The project requires .NET 10 SDK. Install it and rerun this file.'
        }
    }

    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    if ($SqlServer) {
        Info 'SQL Server LocalDB mode selected.'
        $env:Database__Provider = 'SqlServer'
        if (-not (Get-Command sqllocaldb -ErrorAction SilentlyContinue)) {
            throw 'SQL Server LocalDB not detected. Install SQL Server Express LocalDB, or use START_DARECARE.cmd for zero-SQL demo mode.'
        }
        & sqllocaldb info MSSQLLocalDB *> $null
        if ($LASTEXITCODE -ne 0) { & sqllocaldb create MSSQLLocalDB | Out-Host }
        & sqllocaldb start MSSQLLocalDB | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'Could not start SQL Server LocalDB.' }
        $existing = (& dotnet user-secrets list --project $api) -join "`n"
        if ($existing -match '(?m)^ConnectionStrings:DefaultConnection\s*=.*DareCareSprint[234]Db') {
            # Preserve the existing Sprint 3 database rather than running new schema migrations over it.
            Info 'Switching the previous generated LocalDB connection to an isolated Sprint 5 demo database.'
            Run-Dotnet @('user-secrets','set','ConnectionStrings:DefaultConnection','Server=(localdb)\MSSQLLocalDB;Database=DareCareSprint5Db;Trusted_Connection=True;TrustServerCertificate=True','--project',$api)
        } elseif ($existing -notmatch '(?m)^ConnectionStrings:DefaultConnection\s*=') {
            Run-Dotnet @('user-secrets','set','ConnectionStrings:DefaultConnection','Server=(localdb)\MSSQLLocalDB;Database=DareCareSprint5Db;Trusted_Connection=True;TrustServerCertificate=True','--project',$api)
        }
    } else {
        Info 'Zero-config SQLite demo mode selected. SQL Server is still available using START_SQLSERVER.cmd.'
        $env:Database__Provider = 'Sqlite'
    }

    Info 'Restoring NuGet packages (internet is needed on first use)...'
    Run-Dotnet @('restore',$solution,'--verbosity','quiet')

    # Development secrets are stored by dotnet user-secrets, not inside the source code.
    $saved = (& dotnet user-secrets list --project $api 2>$null) -join "`n"
    if ($saved -notmatch '(?m)^Jwt:Key\s*=') {
        [byte[]]$bytes = New-Object byte[] 48
        $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
        try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
        $key = [Convert]::ToBase64String($bytes)
        Run-Dotnet @('user-secrets','set','Jwt:Key',$key,'--project',$api)
        Info 'Generated a strong local JWT signing key.'
    }

    $credentialsFile = Join-Path $localDir 'admin-login.txt'
    if (-not (Test-Path $credentialsFile)) {
        [byte[]]$passwordBytes = New-Object byte[] 28
        $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
        try { $rng.GetBytes($passwordBytes) } finally { $rng.Dispose() }
        $password = 'Dc!9' + [Convert]::ToBase64String($passwordBytes)
        @("Email=admin@darecare.local", "Password=$password") | Set-Content -Path $credentialsFile -Encoding UTF8
        Info 'Created a LOCAL demo admin password in .localdata\admin-login.txt. NEVER share this file.'
    }
    $account = @{}
    foreach ($line in (Get-Content $credentialsFile)) {
        if ($line -match '^([^=]+)=(.*)$') { $account[$matches[1]] = $matches[2] }
    }
    if (-not $account.ContainsKey('Email') -or -not $account.ContainsKey('Password')) {
        throw 'Invalid .localdata\admin-login.txt file. Repair it or delete only that file to generate new credentials for a NEW database.'
    }
    $env:BootstrapAdmin__Email = $account.Email
    $env:BootstrapAdmin__Password = $account.Password

    Info 'Compiling backend...'
    Run-Dotnet @('build',$solution,'--no-restore','--verbosity','minimal')
    Info 'Running unit tests...'
    Run-Dotnet @('test',$solution,'--no-build','--verbosity','minimal')

    if ($SqlServer) {
        Info 'Preparing SQL Server schema with EF Core migrations...'
        Run-Dotnet @('tool','restore')
        $migrationDir = Join-Path $infra 'Persistence\Migrations'
        if (-not (Test-Path $migrationDir) -or -not (Get-ChildItem $migrationDir -Filter '*_InitialCreate.cs' -ErrorAction SilentlyContinue)) {
            Run-Dotnet @('ef','migrations','add','InitialCreate','--project',$infra,'--startup-project',$api,'--output-dir','Persistence/Migrations')
        } elseif (-not (Get-ChildItem $migrationDir -Filter '*_AddSprint5Modules.cs' -ErrorAction SilentlyContinue)) {
            # Preserve previous migration history and scaffold only the new schema delta.
            Run-Dotnet @('ef','migrations','add','AddSprint5Modules','--project',$infra,'--startup-project',$api,'--output-dir','Persistence/Migrations')
        }
        Run-Dotnet @('ef','database','update','--project',$infra,'--startup-project',$api)
    }
    if ($TestOnly) { Info 'Build and tests finished.'; exit 0 }

    $existingApp = Get-Health
    if ($null -ne $existingApp -and -not (Is-Ready)) {
        throw 'A different DareCare database mode is already using port 5208. Close it with STOP_DARECARE.cmd first.'
    }
    if (Is-Ready) {
        Info 'API already running on port 5208. Opening development page...'
        Start-Process 'http://127.0.0.1:5208/dev/auth'
        Info ('Admin credentials: ' + $credentialsFile)
        exit 0
    }
    Info 'Starting DareCare API on localhost:5208...'
    $stdout = Join-Path $localDir 'api.stdout.log'
    $stderr = Join-Path $localDir 'api.stderr.log'
    $argLine = 'run --no-build --project "' + $api + '" --launch-profile http'
    $ownedProcess = Start-Process -FilePath 'dotnet' -ArgumentList $argLine -WorkingDirectory $api `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru -WindowStyle Hidden
    @("Id=$($ownedProcess.Id)", "StartUtcTicks=$($ownedProcess.StartTime.ToUniversalTime().Ticks)") | Set-Content $pidFile -Encoding Ascii
    $ready = $false
    for ($i = 0; $i -lt 100; $i++) {
        if (Is-Ready) { $ready = $true; break }
        if ($ownedProcess.HasExited) { break }
        Start-Sleep -Milliseconds 500
    }
    if (-not $ready) {
        if (Test-Path $stderr) { Get-Content $stderr -Tail 35 | Write-Host }
        if (Test-Path $stdout) { Get-Content $stdout -Tail 35 | Write-Host }
        throw 'API was not healthy. Check the log files in .localdata.'
    }
    Info 'API and local database are running.'
    Info ('Admin test credentials are in: ' + $credentialsFile)
    Info 'Open http://127.0.0.1:5208/dev/auth'
    Start-Process 'http://127.0.0.1:5208/dev/auth'
    Info 'Leave this window open while testing. Close it with Ctrl+C, or use STOP_DARECARE.cmd.'
    Wait-Process -Id $ownedProcess.Id
} catch {
    Write-Host "[ERROR] $_" -ForegroundColor Red
    exit 1
} finally {
    if ($ownedProcess -and -not $ownedProcess.HasExited) {
        & taskkill /PID $ownedProcess.Id /T /F *> $null
    }
    if ($ownedProcess -and (Test-Path $pidFile)) {
        $trackedId = (Get-Content $pidFile | Where-Object { $_ -like 'Id=*' } | Select-Object -First 1)
        if ($trackedId -eq "Id=$($ownedProcess.Id)") {
            Remove-Item $pidFile -Force -ErrorAction SilentlyContinue
        }
    }
}
