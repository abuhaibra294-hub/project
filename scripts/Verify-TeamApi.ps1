[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://127.0.0.1:5208',
    [string]$FrontendOrigin = 'http://localhost:5173'
)
$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')
$uri = [uri]$BaseUrl
if ($uri.Scheme -ne 'https' -and -not ($uri.Scheme -eq 'http' -and $uri.IsLoopback)) {
    throw 'Only HTTPS remote Base URLs are allowed.'
}
function Check([string]$name, [string]$method, [string]$path, [int]$expected) {
    $headers=@{ Accept='application/json' }
    $status=0
    try {
        $response=Invoke-WebRequest -Method $method -Uri "$BaseUrl$path" -Headers $headers -UseBasicParsing -TimeoutSec 15
        $status=[int]$response.StatusCode
    } catch {
        if ($_.Exception.Response) { $status=[int]$_.Exception.Response.StatusCode } else { throw }
    }
    if ($status -ne $expected) { throw "$name failed: expected HTTP $expected, got $status" }
    Write-Host "[PASS] $name -> HTTP $status" -ForegroundColor Green
}
Check 'API process alive' 'GET' '/health' 200
Check 'Database ready' 'GET' '/health/ready' 200
Check 'Public skills catalog' 'GET' '/api/v1/skills' 200
Check 'Public nurses discovery' 'GET' '/api/v1/nurses/search?page=1&pageSize=5' 200
Check 'Anonymous user blocked from account' 'GET' '/api/v1/auth/me' 401
Check 'Anonymous user blocked from admin' 'GET' '/api/v1/admin/overview' 401
if ($FrontendOrigin) {
    $headers=@{ 'Origin'=$FrontendOrigin; 'Access-Control-Request-Method'='GET' }
    $res=Invoke-WebRequest -Method Options -Uri "$BaseUrl/api/v1/nurses/search" -Headers $headers -UseBasicParsing -TimeoutSec 15
    $allowed=[string]$res.Headers['Access-Control-Allow-Origin']
    if ($allowed -ne $FrontendOrigin) { throw "CORS Origin mismatch: expected $FrontendOrigin, got '$allowed'. Configure Cors__AllowedOrigins__0." }
    Write-Host '[PASS] Allowed browser origin matches CORS config.' -ForegroundColor Green
}
Write-Host 'BASIC HTTP CONTRACT CHECKS PASS. Run SMOKE_DARECARE.cmd for an E2E simulation.' -ForegroundColor Cyan
