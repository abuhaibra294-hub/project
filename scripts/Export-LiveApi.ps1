[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://127.0.0.1:5208',
    [switch]$SkipRouteCheck
)
$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')
$uri = [uri]$BaseUrl
if ($uri.Scheme -ne 'https' -and -not ($uri.Scheme -eq 'http' -and $uri.IsLoopback)) {
    throw 'Use HTTPS for a remote server. Plain HTTP is allowed only for localhost.'
}
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$local = Join-Path $root '.localdata'
New-Item -ItemType Directory -Path $local -Force | Out-Null
$expected = Get-Content -Raw -Path (Join-Path $root 'integration\DareCare_API_Endpoints.json') | ConvertFrom-Json
$url = "$BaseUrl/openapi/v1.json"
Write-Host "Fetching LIVE OpenAPI from $url" -ForegroundColor Cyan
try { $resp = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 20 -ErrorAction Stop }
catch { throw 'API OpenAPI endpoint unavailable. Start local Development or enable ApiDocs:EnableInStaging on Staging. Check connection and TLS. ' + $_ }
if ($resp.StatusCode -ne 200) { throw "OpenAPI returned HTTP $($resp.StatusCode)" }
$spec = $resp.Content | ConvertFrom-Json
if (-not $spec.openapi -or -not $spec.paths) { throw 'Response is not a valid OpenAPI JSON document.' }
$out = Join-Path $local 'DareCare_OpenAPI_LIVE.json'
# Write only a live machine-readable schema, never any token/credentials.
[IO.File]::WriteAllText($out, [string]$resp.Content, [Text.UTF8Encoding]::new($false))
Write-Host "Saved LIVE schema: $out" -ForegroundColor Green
if (-not $SkipRouteCheck) {
    $actual = @{}
    foreach ($path in $spec.paths.PSObject.Properties) {
        foreach ($verb in $path.Value.PSObject.Properties) {
            if ($verb.Name -in @('get','post','patch','put','delete')) {
                $key = ($verb.Name.ToUpperInvariant() + ' ' + ($path.Name -replace ':guid(?=})',''))
                $actual[$key] = $true
            }
        }
    }
    $missing = @()
    foreach ($item in $expected.endpoints) {
        $key = $item.method + ' ' + $item.path
        if (-not $actual.ContainsKey($key)) { $missing += $key }
    }
    if ($missing.Count -gt 0) {
        Write-Host 'Missing LIVE routes:' -ForegroundColor Red
        $missing | ForEach-Object { Write-Host " - $_" }
        throw "$($missing.Count) expected routes missing from LIVE OpenAPI. The deployment may be on an older build."
    }
    Write-Host "PASS: All $($expected.count) checked-in controller routes were found in the LIVE spec." -ForegroundColor Green
}
