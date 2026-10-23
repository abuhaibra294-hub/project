# DareCare first-time private GitHub publish helper.
# Creates a private DareCare.Backend repository and pushes source, without touching unrelated repos.
# Windows PowerShell 5.1+; requires internet and browser login for GitHub authorization.
param(
    [string]$RepoName = 'DareCare.Backend',
    [string]$ExpectedLogin = 'abuhaibra294-hub'
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root

function Info([string]$message) { Write-Host "[DareCare] $message" -ForegroundColor Cyan }
function Fail([string]$message) { throw "[DareCare] $message" }
function Refresh-Path {
    $paths = @(
        "$env:ProgramFiles\Git\cmd",
        "$env:ProgramFiles\GitHub CLI",
        "${env:ProgramFiles(x86)}\Git\cmd",
        "$env:LOCALAPPDATA\Programs\Git\cmd"
    )
    foreach ($path in $paths) {
        if ($path -and (Test-Path $path) -and ($env:Path -notlike "*$path*")) { $env:Path += ";$path" }
    }
}
function Ensure-Cli([string]$command, [string]$packageId, [string]$manualLink) {
    if (Get-Command $command -ErrorAction SilentlyContinue) { return }
    Info "$command is not installed. Attempting a standard winget installation..."
    if (Get-Command winget -ErrorAction SilentlyContinue) {
        & winget install --id $packageId --exact --source winget --accept-source-agreements --accept-package-agreements
        Refresh-Path
    }
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        Fail "Please install $command from $manualLink, reopen this CMD, and run again."
    }
}
function Run([string]$program, [string[]]$arguments) {
    & $program @arguments
    if ($LASTEXITCODE -ne 0) { Fail "Command failed ($LASTEXITCODE): $program $($arguments -join ' ')" }
}
function Test-Native([string]$program, [string[]]$arguments) {
    try {
        & $program @arguments *> $null
        return ($LASTEXITCODE -eq 0)
    } catch { return $false }
}

if (-not (Test-Path (Join-Path $root 'DareCare.slnx'))) { Fail 'DareCare.slnx missing. Extract the whole ZIP first.' }
if (-not (Test-Path (Join-Path $root 'render.yaml'))) { Fail 'render.yaml missing. Use Sprint 6 or later.' }
Ensure-Cli 'git' 'Git.Git' 'https://git-scm.com/downloads/win'
Ensure-Cli 'gh' 'GitHub.cli' 'https://cli.github.com/'

Info 'Checking GitHub login...'
if (-not (Test-Native 'gh' @('auth', 'status', '--hostname', 'github.com'))) {
    Info 'A browser-based GitHub sign-in is required. This script never asks you for a token or password.'
    Run 'gh' @('auth', 'login', '--hostname', 'github.com', '--web', '--git-protocol', 'https')
}
Run 'gh' @('auth', 'setup-git')
$login = (& gh api user --jq '.login' | Select-Object -First 1).Trim()
if ($LASTEXITCODE -ne 0 -or -not $login) { Fail 'Could not verify the GitHub account.' }
if ($login -ne $ExpectedLogin) { Fail "Signed into '$login', but expected '$ExpectedLogin'. Switch account with 'gh auth login' first." }

$repo = "$login/$RepoName"
Info "Target private repository: $repo"
if (Test-Path (Join-Path $root '.git')) {
    $origin = ''
    if (Test-Native 'git' @('remote', 'get-url', 'origin')) { $origin = (& git remote get-url origin | Select-Object -First 1) }
    if ($origin -and ($origin -notmatch "(?i)(^|[:/])$([regex]::Escape($login))/$([regex]::Escape($RepoName))(\.git)?$")) {
        Fail "This folder already points to a DIFFERENT remote ($origin). No changes made."
    }
} else {
    Run 'git' @('init', '-b', 'main')
}

# All secrets, databases and prescription files must be excluded by .gitignore.
Run 'git' @('add', '--all')
$tracked = @(& git ls-files)
$blocked = @($tracked | Where-Object {
    $_ -match '(?i)(^|/)(\.env(\..+)?|\.localdata|App_Data|secrets\.json)(/|$)' -and $_ -notmatch '(?i)(^|/)(\.env\.example|\.env\.staging\.example)$' -or
    $_ -match '(?i)(\.db|\.sqlite|\.sqlite3|\.pfx|\.pem|\.key|\.p12)$'
})
if ($blocked.Count -gt 0) { Fail "Unsafe files staged: $($blocked -join ', '). Remove them before publishing." }
if ($tracked.Count -lt 80) { Fail "Only $($tracked.Count) files staged; expected the complete project. Check the extracted ZIP and .gitignore." }

$author = (& git config --local user.name)
if (-not $author) { Run 'git' @('config', '--local', 'user.name', $login) }
$mail = (& git config --local user.email)
if (-not $mail) {
    $userId = (& gh api user --jq '.id' | Select-Object -First 1).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $userId) { Fail 'Could not read GitHub user ID for commit email.' }
    Run 'git' @('config', '--local', 'user.email', "$userId+$login@users.noreply.github.com")
}

& git diff --cached --quiet
if ($LASTEXITCODE -eq 1) {
    Run 'git' @('commit', '-m', 'Initial DareCare Backend Sprint 6 with Render integration')
} elseif ($LASTEXITCODE -ne 0) { Fail 'Could not inspect staged changes.' }

Info 'Checking whether repository already exists...'
$exists = Test-Native 'gh' @('repo', 'view', $repo, '--json', 'name,isPrivate')
if ($exists) {
    $isPrivate = (& gh repo view $repo --json isPrivate --jq '.isPrivate').Trim()
    if ($LASTEXITCODE -ne 0 -or $isPrivate -ne 'true') { Fail "Repository $repo is not private. Refusing to publish." }
    $heads = @(& gh api "repos/$repo/branches" --jq '.[].name')
    if ($LASTEXITCODE -ne 0) { Fail "Could not safely inspect existing branches of $repo." }
    if ($heads.Count -gt 0) { Fail "Repository $repo already has branches. Refusing to overwrite existing work. Use a fresh repository name." }
    if (-not (Test-Native 'git' @('remote', 'get-url', 'origin'))) { Run 'git' @('remote', 'add', 'origin', "https://github.com/$repo.git") }
    Info 'Existing private repository is empty; pushing first main branch...'
    Run 'git' @('push', '-u', 'origin', 'HEAD:main')
} else {
    Info 'Creating NEW private repository and pushing project...'
    Run 'gh' @('repo', 'create', $repo, '--private', '--description', 'DareCare ASP.NET Core graduation project backend', '--source', $root, '--remote', 'origin', '--push')
}

Info 'Verifying the GitHub upload...'
$found = (& gh api "repos/$repo/contents/render.yaml" --jq '.path' | Select-Object -First 1).Trim()
if ($LASTEXITCODE -ne 0 -or $found -ne 'render.yaml') { Fail 'Repository exists, but render.yaml was not found. Verify push before using Render.' }
$visibility = (& gh repo view $repo --json isPrivate --jq '.isPrivate').Trim()
if ($visibility -ne 'true') { Fail 'Repository visibility is not private. Stop and inspect it immediately.' }
Write-Host ''
Write-Host "SUCCESS: https://github.com/$repo" -ForegroundColor Green
Write-Host 'Next: GitHub Actions > check the .NET CI run, then Render > New > Blueprint.' -ForegroundColor Yellow
Start-Process "https://github.com/$repo"
