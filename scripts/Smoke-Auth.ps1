# Local-only REST API smoke test. No real patient information or fixed passwords.
param([string]$BaseUrl = 'http://localhost:5208')
$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')
$stamp = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
$randomBytes = New-Object byte[] 24
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($randomBytes) } finally { $rng.Dispose() }
$password = [Convert]::ToBase64String($randomBytes) + 'aA1!'
$email = "smoke-$stamp@darecare.test"

function Send([string]$method, [string]$path, $payload = $null, [string]$token = '', [int]$expected = 200) {
    $headers = @{ Accept = 'application/json' }
    if ($token) { $headers['Authorization'] = 'Bearer ' + $token }
    $args = @{ Uri = "$BaseUrl$path"; Method = $method; Headers = $headers; ErrorAction = 'Stop'; UseBasicParsing = $true }
    if ($null -ne $payload) {
        $args.ContentType = 'application/json'
        $args.Body = ConvertTo-Json -InputObject $payload -Depth 8 -Compress
    }
    $code = 0; $data = $null
    try {
        $result = Invoke-WebRequest @args
        $code = [int]$result.StatusCode
        if ($result.Content) { $data = $result.Content | ConvertFrom-Json }
    }
    catch {
        if ($null -eq $_.Exception.Response) { throw }
        $code = [int]$_.Exception.Response.StatusCode
    }
    if ($code -ne $expected) { throw "$method $path expected $expected, got $code" }
    Write-Host "[PASS] $method $path -> $code"
    return $data
}

Send 'GET' '/health' | Out-Null
$registered = Send 'POST' '/api/v1/auth/register/patient' @{
    fullName = 'Sample Patient'; email = $email; password = $password; phoneNumber = '+201000000000'
}
if (-not $registered.accessToken -or -not $registered.refreshToken) {
    throw 'This smoke test requires Development with Auth:RequireConfirmedEmail=false.'
}
$me = Send 'GET' '/api/v1/auth/me' $null $registered.accessToken
if ($me.email -ne $email) { throw 'Wrong account returned by /me' }
$profile = Send 'GET' '/api/v1/patients/me' $null $registered.accessToken
Send 'PUT' '/api/v1/patients/me' @{ city = 'Mansoura'; defaultAddress = 'Demo Street 1' } $registered.accessToken | Out-Null
Send 'GET' '/api/v1/nurses/me' $null $registered.accessToken 403 | Out-Null
Send 'POST' '/api/v1/auth/login' @{ email = $email; password = 'WrongPassword!123' } '' 401 | Out-Null
$login = Send 'POST' '/api/v1/auth/login' @{ email = $email; password = $password }
$rotated = Send 'POST' '/api/v1/auth/refresh' @{ refreshToken = $login.refreshToken }
if ($login.refreshToken -eq $rotated.refreshToken) { throw 'Refresh token did not rotate' }
Send 'POST' '/api/v1/auth/logout' @{ refreshToken = $rotated.refreshToken } $rotated.accessToken 204 | Out-Null
Send 'POST' '/api/v1/auth/refresh' @{ refreshToken = $rotated.refreshToken } '' 401 | Out-Null
Send 'POST' '/api/v1/auth/logout-all' $null $registered.accessToken 204 | Out-Null
Send 'GET' '/api/v1/auth/me' $null $registered.accessToken 401 | Out-Null
Send 'POST' '/api/v1/auth/refresh' @{ refreshToken = $registered.refreshToken } '' 401 | Out-Null
Write-Host 'Authentication smoke test succeeded. Generated account contains synthetic data only.'
