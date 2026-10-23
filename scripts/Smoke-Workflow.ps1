# Development-only synthetic end-to-end test. Do not use real health information.
[CmdletBinding()]
param([string]$BaseUrl = 'http://127.0.0.1:5208', [switch]$SessionDemo)
$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$adminFile = Join-Path $projectRoot '.localdata\admin-login.txt'
if (-not (Test-Path $adminFile)) { throw 'Double-click START_DARECARE.cmd first, and leave it running.' }
$account = @{}
foreach ($line in (Get-Content $adminFile)) {
    if ($line -match '^([^=]+)=(.*)$') { $account[$matches[1]] = $matches[2] }
}
if (-not $account.Email -or -not $account.Password) { throw 'Admin file is missing Email/Password.' }

function Send([string]$method, [string]$path, $payload = $null, [string]$token = '', [int]$expected = 200) {
    $headers = @{ Accept = 'application/json' }
    if ($token) { $headers['Authorization'] = 'Bearer ' + $token }
    $params = @{ Uri = "$BaseUrl$path"; Method = $method; Headers = $headers; ErrorAction = 'Stop'; UseBasicParsing = $true }
    if ($null -ne $payload) {
        $params.ContentType = 'application/json'
        $params.Body = ConvertTo-Json -InputObject $payload -Depth 10 -Compress
    }
    $status = 0; $content = ''
    try {
        $response = Invoke-WebRequest @params
        $status = [int]$response.StatusCode
        $content = $response.Content
    } catch {
        if ($null -eq $_.Exception.Response) { throw }
        $status = [int]$_.Exception.Response.StatusCode
    }
    if ($status -ne $expected) { throw "$method $path expected $expected, got $status" }
    Write-Host "[PASS] $method $path -> $status" -ForegroundColor Green
    if ($content) { return ($content | ConvertFrom-Json) }
    return $null
}

function RandomPassword {
    [byte[]]$bytes = New-Object byte[] 22
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    return 'Dc!9' + [Convert]::ToBase64String($bytes)
}

function Upload-SyntheticImage([string]$patientToken, [string]$pharmacyId, [string]$bookingId) {
    $boundary = '----DareCareSmoke' + [guid]::NewGuid().ToString('N')
    $prefix = "--$boundary`r`nContent-Disposition: form-data; name=`"pharmacyProfileId`"`r`n`r`n$pharmacyId`r`n" +
        "--$boundary`r`nContent-Disposition: form-data; name=`"bookingId`"`r`n`r`n$bookingId`r`n" +
        "--$boundary`r`nContent-Disposition: form-data; name=`"file`"; filename=`"demo.png`"`r`nContent-Type: image/png`r`n`r`n"
    $suffix = "`r`n--$boundary--`r`n"
    # A real 2x2 PNG with no patient information.
    $png = [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAEElEQVR4nGP48OcvEDFAKABRVguluTu3nAAAAABJRU5ErkJggg==')
    [byte[]]$multipart = [Text.Encoding]::UTF8.GetBytes($prefix) + $png + [Text.Encoding]::UTF8.GetBytes($suffix)
    $res = Invoke-WebRequest -Uri "$BaseUrl/api/v1/prescriptions" -Method Post `
        -ContentType "multipart/form-data; boundary=$boundary" `
        -Headers @{ Authorization = 'Bearer ' + $patientToken; Accept = 'application/json' } `
        -Body $multipart -UseBasicParsing
    if ([int]$res.StatusCode -ne 201) { throw 'Prescription upload did not return 201.' }
    Write-Host '[PASS] POST /api/v1/prescriptions -> 201' -ForegroundColor Green
    return ($res.Content | ConvertFrom-Json)
}

$stamp = [Guid]::NewGuid().ToString('N').Substring(0,12)
$patientEmail = "patient-$stamp@darecare.test"
$nurseEmail = "nurse-$stamp@darecare.test"
$pharmacyEmail = "pharmacy-$stamp@darecare.test"
$password = RandomPassword

Send 'GET' '/health' | Out-Null
$admin = Send 'POST' '/api/v1/auth/login' @{ email = $account.Email; password = $account.Password }
$adminToken = $admin.accessToken
$patient = Send 'POST' '/api/v1/auth/register/patient' @{
    fullName = 'Synthetic Patient'; email = $patientEmail; password = $password; phoneNumber = '+201000000001'
}
$nurse = Send 'POST' '/api/v1/auth/register/nurse' @{
    fullName = 'Synthetic Nurse'; email = $nurseEmail; password = $password;
    phoneNumber = '+201000000002'; licenseNumber = "DEVNURSE-$stamp"; yearsOfExperience = 5
}
$patientToken = $patient.accessToken
$nurseToken = $nurse.accessToken
# Sprint 5: two saved addresses, ownership and default switching.
$home = Send 'POST' '/api/v1/patients/me/addresses' @{
    label = 'Home'; city = 'Mansoura'; fullAddress = 'Demo road 3, building 7'; isDefault = $true
} $patientToken 201
$work = Send 'POST' '/api/v1/patients/me/addresses' @{
    label = 'Work'; city = 'Mansoura'; fullAddress = 'Demo road 9, building 5'; isDefault = $false
} $patientToken 201
Send 'PUT' "/api/v1/patients/me/addresses/$($work.id)/default" $null $patientToken | Out-Null
$allAddresses = Send 'GET' '/api/v1/patients/me/addresses' $null $patientToken
if (@($allAddresses | Where-Object { $_.isDefault }).Count -ne 1) { throw 'Exactly one default address is required.' }
Send 'GET' "/api/v1/patients/me/addresses/$($home.id)" $null $patientToken | Out-Null
if (-not $patientToken -or -not $nurseToken) { throw 'Expected immediate tokens in Development only.' }
Send 'GET' '/api/v1/admin/overview' $null $patientToken 403 | Out-Null
$skills = Send 'GET' '/api/v1/skills'
$services = Send 'GET' '/api/v1/services'
$service = $services | Where-Object { $_.requiredSkillId -eq $skills[0].id } | Select-Object -First 1
if (-not $service) { $service = $services | Select-Object -First 1 }
Send 'PUT' '/api/v1/nurses/me' @{
    bio = 'Synthetic demo nurse'; city = 'Mansoura'; serviceArea = 'Mansoura';
    hourlyRate = 200; yearsOfExperience = 5
} $nurseToken | Out-Null
Send 'PUT' '/api/v1/nurses/me/skills' @{ skillIds = @($service.requiredSkillId) } $nurseToken | Out-Null
$nurseProfile = Send 'GET' '/api/v1/nurses/me' $null $nurseToken
Send 'PATCH' "/api/v1/admin/nurses/$($nurseProfile.id)/approval" @{ approved = $true } $adminToken | Out-Null
$start = [DateTime]::UtcNow.AddHours(3)
Send 'GET' '/api/v1/nurses/search?city=Mansoura&pageSize=5' | Out-Null
Send 'GET' '/api/v1/nurses/search?pageSize=999' $null '' 400 | Out-Null
Send 'GET' "/api/v1/nurses/$($nurseProfile.id)" | Out-Null
Send 'GET' '/api/v1/services/search?pageSize=5' | Out-Null
Send 'POST' "/api/v1/favorites/nurses/$($nurseProfile.id)" $null $patientToken 201 | Out-Null
$favorite = Send 'GET' "/api/v1/favorites/nurses/$($nurseProfile.id)" $null $patientToken
if (-not $favorite.isFavorite) { throw 'Saved favorite was not found.' }
Send 'GET' '/api/v1/favorites/nurses' $null $patientToken | Out-Null
$slot = Send 'POST' '/api/v1/nurses/me/slots' @{
    startsAt = $start.ToString('o'); endsAt = $start.AddMinutes(90).ToString('o')
} $nurseToken 201
Send 'GET' '/api/v1/nurses/me/slots' $null $nurseToken | Out-Null
$recommendations = Send 'GET' "/api/v1/matching/nurses?serviceId=$($service.id)&city=Mansoura"
if (-not ($recommendations.recommendations | Where-Object { $_.id -eq $nurseProfile.id })) {
    throw 'Nurse not returned by matching.'
}
$booking = Send 'POST' '/api/v1/bookings' @{
    careServiceId = $service.id; nurseProfileId = $nurseProfile.id;
    availabilitySlotId = $slot.id; addressId = $work.id
} $patientToken 201
Send 'POST' "/api/v1/bookings/$($booking.id)/accept" $null $nurseToken | Out-Null
Send 'POST' "/api/v1/bookings/$($booking.id)/start" $null $nurseToken | Out-Null
Send 'POST' "/api/v1/bookings/$($booking.id)/complete" $null $nurseToken | Out-Null
$bookingTimeline = Send 'GET' "/api/v1/bookings/$($booking.id)/timeline" $null $patientToken
if (@($bookingTimeline).Count -lt 4) { throw 'Booking timeline events missing.' }
$bookingsSearch = Send 'GET' '/api/v1/bookings/mine/search?status=Completed&pageSize=5' $null $patientToken
if ($bookingsSearch.total -lt 1) { throw 'Bookings search did not include completed visit.' }

$review = Send 'POST' "/api/v1/reviews/bookings/$($booking.id)" @{
    rating = 5; comment = 'Synthetic five-star test, no patient details'
} $patientToken 201
Send 'POST' "/api/v1/reviews/bookings/$($booking.id)" @{ rating = 1 } $patientToken 409 | Out-Null
Send 'GET' "/api/v1/reviews/$($review.id)" $null $nurseToken 403 | Out-Null
$otherPatient = Send 'POST' '/api/v1/auth/register/patient' @{
    fullName = 'Other Synthetic Patient'; email = "other-patient-$stamp@darecare.test";
    password = $password; phoneNumber = '+201000000004'
}
Send 'GET' "/api/v1/reviews/$($review.id)" $null $otherPatient.accessToken 403 | Out-Null
Send 'POST' "/api/v1/reviews/bookings/$($booking.id)" @{ rating = 5 } $otherPatient.accessToken 403 | Out-Null
Send 'GET' "/api/v1/patients/me/addresses/$($work.id)" $null $otherPatient.accessToken 404 | Out-Null
$ticket = Send 'POST' '/api/v1/support/tickets' @{
    subject = 'Synthetic booking question'; category = 'Booking';
    message = 'This is a synthetic question for an automated test.'
} $patientToken 201
Send 'GET' "/api/v1/support/tickets/$($ticket.id)" $null $otherPatient.accessToken 403 | Out-Null
Send 'GET' '/api/v1/support/tickets/mine?pageSize=5' $null $patientToken | Out-Null
Send 'GET' '/api/v1/support/admin/tickets?status=Open' $null $patientToken 403 | Out-Null
Send 'GET' '/api/v1/support/admin/tickets?status=Open' $null $adminToken | Out-Null
Send 'PATCH' "/api/v1/support/admin/tickets/$($ticket.id)/status" @{ status = 'InProgress' } $adminToken | Out-Null
Send 'POST' "/api/v1/support/tickets/$($ticket.id)/messages" @{
    message = 'Our synthetic support team has received your request.'
} $adminToken | Out-Null
Send 'POST' "/api/v1/support/tickets/$($ticket.id)/messages" @{
    message = 'Thank you for reviewing this synthetic request.'
} $patientToken | Out-Null
Send 'PATCH' "/api/v1/support/admin/tickets/$($ticket.id)/status" @{ status = 'Resolved' } $adminToken | Out-Null
Send 'PATCH' "/api/v1/support/admin/tickets/$($ticket.id)/status" @{ status = 'Closed' } $adminToken | Out-Null
Send 'POST' "/api/v1/support/tickets/$($ticket.id)/messages" @{ message = 'After closed' } $patientToken 409 | Out-Null
Send 'DELETE' "/api/v1/favorites/nurses/$($nurseProfile.id)" $null $patientToken 204 | Out-Null

$reviewSummary = Send 'GET' "/api/v1/reviews/nurses/$($nurseProfile.id)/summary"
if ($reviewSummary.count -ne 1 -or $reviewSummary.averageRating -ne 5) { throw 'Nurse review aggregate is incorrect.' }
# The first visit has consumed its slot; add a second free slot for fresh recommendations.
$nextStart = [DateTime]::UtcNow.AddHours(6)
$nextSlot = Send 'POST' '/api/v1/nurses/me/slots' @{
    startsAt = $nextStart.ToString('o'); endsAt = $nextStart.AddMinutes(90).ToString('o')
} $nurseToken 201
$afterRating = Send 'GET' "/api/v1/matching/nurses?serviceId=$($service.id)&city=Mansoura"
$rankedNurse = $afterRating.recommendations | Where-Object { $_.id -eq $nurseProfile.id } | Select-Object -First 1
if (-not $rankedNurse -or $rankedNurse.reviewCount -ne 1) { throw 'Matching did not include rating aggregate.' }
# Old free-text booking format continues to work alongside saved addressId.
$freeTextBooking = Send 'POST' '/api/v1/bookings' @{
    careServiceId = $service.id; nurseProfileId = $nurseProfile.id;
    availabilitySlotId = $nextSlot.id; serviceAddress = 'Legacy synthetic address in Mansoura'
} $patientToken 201
Send 'POST' "/api/v1/bookings/$($freeTextBooking.id)/cancel" $null $patientToken | Out-Null
Send 'GET' '/api/v1/dashboard/me' $null $patientToken | Out-Null
Send 'GET' '/api/v1/dashboard/me' $null $nurseToken | Out-Null

$pharmacy = Send 'POST' '/api/v1/admin/pharmacies' @{
    email = $pharmacyEmail; businessName = "Synthetic Pharmacy $stamp";
    licenseNumber = "DEVPHA-$stamp"; contactName = 'Synthetic Pharmacist';
    phoneNumber = '+201000000003'; city = 'Mansoura'; address = 'Synthetic Pharmacy Address'
} $adminToken 201
$mail = Send 'GET' '/api/v1/dev/mailbox' $null $adminToken
$invite = $mail | Where-Object { $_.to -eq $pharmacyEmail -and $_.subject -like '*password*' } | Select-Object -First 1
if (-not $invite) { throw 'Development invitation email was not found. Disable SMTP for this test.' }
$code = ($invite.body -split "`n" | Where-Object { $_ -match '^[A-Za-z0-9_-]{40,}$' } | Select-Object -First 1).Trim()
if (-not $code) { throw 'Could not parse one-time invitation code.' }
Send 'POST' '/api/v1/auth/reset-password' @{
    email = $pharmacyEmail; code = $code; newPassword = $password
} | Out-Null
Send 'PATCH' "/api/v1/admin/pharmacies/$($pharmacy.id)/approval" @{ approved = $true } $adminToken | Out-Null
$pharmacist = Send 'POST' '/api/v1/auth/login' @{ email = $pharmacyEmail; password = $password }
$pharmacyToken = $pharmacist.accessToken
Send 'GET' '/api/v1/pharmacies/available' | Out-Null
$rx = Upload-SyntheticImage $patientToken $pharmacy.id $booking.id
Send 'GET' "/api/v1/prescriptions/$($rx.id)/file" $null $nurseToken 403 | Out-Null
Send 'GET' '/api/v1/prescriptions/inbox' $null $pharmacyToken | Out-Null
$order = Send 'POST' "/api/v1/orders/prescriptions/$($rx.id)/quote" @{
    amount = 145.50; notes = 'Synthetic pharmacist reviewed order'
} $pharmacyToken 201
Send 'POST' "/api/v1/orders/$($order.id)/prepare" $null $pharmacyToken 409 | Out-Null
Send 'POST' "/api/v1/orders/$($order.id)/confirm" $null $patientToken | Out-Null
Send 'POST' "/api/v1/orders/$($order.id)/prepare" $null $pharmacyToken | Out-Null
Send 'POST' "/api/v1/orders/$($order.id)/dispatch" $null $pharmacyToken | Out-Null
$delivered = Send 'POST' "/api/v1/orders/$($order.id)/deliver" $null $pharmacyToken
if ($delivered.status -ne 'Delivered') { throw 'Order was not delivered.' }
$notices = Send 'GET' '/api/v1/notifications' $null $patientToken
if (-not $notices -or $notices.Count -lt 1) { throw 'Expected notifications for patient.' }
$unread = Send 'GET' '/api/v1/notifications/unread-count' $null $patientToken
if ($unread.unreadCount -lt 1) { throw 'Expected unread notifications.' }
Send 'PATCH' '/api/v1/notifications/read-all' $null $patientToken | Out-Null
$afterRead = Send 'GET' '/api/v1/notifications/unread-count' $null $patientToken
if ($afterRead.unreadCount -ne 0) { throw 'Mark-all-read failed.' }
Send 'GET' '/api/v1/admin/overview' $null $adminToken | Out-Null
Send 'GET' '/api/v1/dashboard/me' $null $pharmacyToken | Out-Null
Send 'GET' '/api/v1/dashboard/me' $null $adminToken | Out-Null
if ($SessionDemo) {
    # This local file contains only random DEMO credentials. Never share/commit it.
    $localDir = Join-Path $projectRoot '.localdata'
    New-Item -ItemType Directory -Path $localDir -Force | Out-Null
    $demoAccounts = Join-Path $localDir 'demo-accounts.txt'
    @(
        'DareCare: SYNTHETIC TEST USERS ONLY - NEVER SHARE THIS FILE',
        "PatientEmail=$patientEmail", "NurseEmail=$nurseEmail", "PharmacyEmail=$pharmacyEmail",
        "Password=$password", "NurseId=$($nurseProfile.id)",
        "BookingId=$($booking.id)", "PharmacyId=$($pharmacy.id)",
        "PrescriptionId=$($rx.id)", "OrderId=$($order.id)"
    ) | Set-Content -Path $demoAccounts -Encoding UTF8
    Write-Host 'Demo test users saved locally in .localdata/demo-accounts.txt (DO NOT SHARE).' -ForegroundColor Yellow
}
Write-Host 'SUCCESS: synthetic Patient -> Addresses -> Favorites -> Nurse -> Booking -> Support -> Pharmacy -> Delivery flow.' -ForegroundColor Cyan
Write-Host 'No real patient data or external payments were used.'
