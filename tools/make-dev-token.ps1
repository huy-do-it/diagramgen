<#
.SYNOPSIS
  Tạo JWT dev (HS256) đúng claim ở TASKS §1.2 để test service khi chưa có Identity.

.EXAMPLE
  ./tools/make-dev-token.ps1 -Role Lecturer -Name lecturer1
  ./tools/make-dev-token.ps1 -UserId 11111111-1111-1111-1111-111111111111 -Role Admin -Name admin

  Dán token vào nút Authorize của Swagger. Ghi lại UserId để thêm MemberAccess / bắn sự kiện đúng người.
  Khóa lấy từ biến môi trường JWT_KEY, mặc định là khóa dev trong deploy/.env.example.
#>
param(
    [guid]$UserId = [guid]::NewGuid(),
    [ValidateSet('Student', 'Lecturer', 'Admin')]
    [string]$Role = 'Student',
    [string]$Name = 'student1',
    [int]$Hours = 8
)

$ErrorActionPreference = 'Stop'

$key = if ($env:JWT_KEY) { $env:JWT_KEY } else { 'dev-only-umlforge-signing-key-change-me-32+bytes' }
$issuer = if ($env:JWT_ISSUER) { $env:JWT_ISSUER } else { 'umlforge' }
$audience = if ($env:JWT_AUDIENCE) { $env:JWT_AUDIENCE } else { 'umlforge-client' }

function ConvertTo-Base64Url([byte[]]$bytes) {
    [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

$utf8 = [Text.Encoding]::UTF8
$now = [DateTimeOffset]::UtcNow
$claims = [ordered]@{
    sub         = "$UserId"
    unique_name = $Name
    role        = $Role
    iss         = $issuer
    aud         = $audience
    iat         = $now.ToUnixTimeSeconds()
    exp         = $now.AddHours($Hours).ToUnixTimeSeconds()
}

$header = ConvertTo-Base64Url $utf8.GetBytes('{"alg":"HS256","typ":"JWT"}')
$payload = ConvertTo-Base64Url $utf8.GetBytes(($claims | ConvertTo-Json -Compress))
$hmac = [Security.Cryptography.HMACSHA256]::new($utf8.GetBytes($key))
try {
    $signature = ConvertTo-Base64Url $hmac.ComputeHash($utf8.GetBytes("$header.$payload"))
}
finally {
    $hmac.Dispose()
}

"UserId: $UserId  ($Name, $Role, hết hạn sau $Hours giờ)"
"$header.$payload.$signature"
