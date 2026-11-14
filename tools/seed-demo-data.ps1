<#
.SYNOPSIS
  Tạo dữ liệu demo CampusEats qua API Gateway (TASKS B-07): project, thành viên, giảng viên và 5 sơ đồ đã render.

.DESCRIPTION
  Gọi API thật như người dùng (không ghi thẳng database, không bắn sự kiện giả) nên dữ liệu khớp nhau ở mọi service:
  Identity → Workspace (project, lời mời, giảng viên) → Diagram (quyền, sơ đồ) → Render → Notification.
  Chạy lại được: project, thành viên, giảng viên và sơ đồ đã có thì bỏ qua; sơ đồ đã render không render lại.
  Dùng tài khoản seed của Identity; mật khẩu đọc từ Seed:DefaultPassword trong appsettings.Development.json.

.EXAMPLE
  ./tools/seed-demo-data.ps1
  ./tools/seed-demo-data.ps1 -GatewayUrl http://localhost:3000 -ProjectName "CampusEats Test"
#>
param(
    # Gateway (:5000) hoặc client nginx (:3000) — cả hai đều chuyển /api tới Gateway
    [string]$GatewayUrl = 'http://localhost:5000',
    [string]$ProjectName = 'CampusEats',
    [string]$Leader = 'student1',
    [string[]]$Members = @('student2', 'student3'),
    [string]$Lecturer = 'lecturer1',
    [string]$DiagramsFile = (Join-Path $PSScriptRoot 'demo-data/campuseats-diagrams.json'),
    [string]$IdentitySettings = (Join-Path (Split-Path -Parent $PSScriptRoot) 'src/Services/Identity/Identity.API/appsettings.Development.json'),
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
$password = (Get-Content -Path $IdentitySettings -Raw -Encoding UTF8 | ConvertFrom-Json).Seed.DefaultPassword
if (-not $password) { throw "Không đọc được Seed:DefaultPassword trong $IdentitySettings." }

function Invoke-Api {
    param([string]$Method, [string]$Path, [string]$Token, $Body)
    $request = @{ Method = $Method; Uri = "$GatewayUrl/api/$Path"; Headers = @{} }
    if ($Token) { $request.Headers.Authorization = "Bearer $Token" }
    if ($null -ne $Body) {
        # Gửi byte UTF-8 để Windows PowerShell 5.1 không làm mất dấu tiếng Việt
        $request.Body = [Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 30 -Compress))
        $request.ContentType = 'application/json; charset=utf-8'
    }
    Invoke-RestMethod @request
}

# Dữ liệu bản sao giữa các service cập nhật qua Redis Streams (eventual consistency) → chờ tới khi điều kiện đúng
function Wait-Until {
    param([scriptblock]$Condition, [string]$What)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try { if (& $Condition) { return } } catch { }
        Start-Sleep -Seconds 2
    }
    throw "Sau $TimeoutSeconds s vẫn chưa $What."
}

function Get-Token([string]$Username) {
    (Invoke-Api -Method POST -Path 'auth/login' -Body @{ username = $Username; password = $password }).accessToken
}

'== 1/5 Chờ Gateway và danh bạ Workspace'
Wait-Until { (Invoke-RestMethod -Uri "$GatewayUrl/health" -TimeoutSec 5) -ne $null } 'Gateway sẵn sàng'
$leaderToken = Get-Token $Leader
$needed = @($Leader) + $Members + @($Lecturer)
$directory = $null
Wait-Until {
    $script:directory = (Invoke-Api -Method GET -Path 'directory/users?pageSize=100' -Token $leaderToken).items
    @($needed | Where-Object { $_ -notin $script:directory.username }).Count -eq 0
} "đồng bộ đủ user ($($needed -join ', ')) sang danh bạ Workspace"
$userId = @{}
foreach ($user in $directory) { $userId[$user.username] = $user.userId }
"   Danh bạ có $($directory.Count) user"

'== 2/5 Project'
$search = [uri]::EscapeDataString($ProjectName)
$project = (Invoke-Api -Method GET -Path "projects?search=$search&pageSize=100" -Token $leaderToken).items |
    Where-Object { $_.name -eq $ProjectName } | Select-Object -First 1
if ($project) {
    "   Đã có project '$ProjectName' ($($project.id))"
}
else {
    $project = Invoke-Api -Method POST -Path 'projects' -Token $leaderToken -Body @{ name = $ProjectName; description = 'Dữ liệu demo tạo bởi tools/seed-demo-data.ps1' }
    "   Tạo project '$ProjectName' ($($project.id)), $Leader là Leader"
}
$projectId = $project.id

'== 3/5 Thành viên và giảng viên'
$memberIds = @((Invoke-Api -Method GET -Path "projects/$projectId/members" -Token $leaderToken).userId)
foreach ($member in $Members) {
    if ($userId[$member] -in $memberIds) { "   $member đã là thành viên"; continue }
    $memberToken = Get-Token $member
    $pending = @(Invoke-Api -Method GET -Path 'invitations/mine' -Token $memberToken) |
        Where-Object { $_.projectId -eq $projectId -and $_.status -eq 'Pending' } | Select-Object -First 1
    if (-not $pending) {
        $pending = Invoke-Api -Method POST -Path "projects/$projectId/invitations" -Token $leaderToken -Body @{ username = $member }
    }
    Invoke-Api -Method POST -Path "invitations/$($pending.id)/accept" -Token $memberToken | Out-Null
    "   $member đã chấp nhận lời mời"
}
$detail = Invoke-Api -Method GET -Path "projects/$projectId" -Token $leaderToken
if ($detail.lecturerId -eq $userId[$Lecturer]) {
    "   $Lecturer đã là giảng viên"
}
else {
    Invoke-Api -Method PUT -Path "projects/$projectId/lecturer" -Token $leaderToken -Body @{ lecturerId = $userId[$Lecturer] } | Out-Null
    "   Gán $Lecturer làm giảng viên"
}

'== 4/5 Chờ Diagram nhận quyền thành viên (sự kiện ProjectCreated/MemberAdded)'
Wait-Until { Invoke-Api -Method GET -Path "diagrams?projectId=$projectId&pageSize=100" -Token $leaderToken } 'đồng bộ quyền sang Diagram'

'== 5/5 Sơ đồ mẫu'
$existing = @((Invoke-Api -Method GET -Path "diagrams?projectId=$projectId&pageSize=100" -Token $leaderToken).items)
$samples = Get-Content -Path $DiagramsFile -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($sample in $samples) {
    $diagram = $existing | Where-Object { $_.title -eq $sample.title } | Select-Object -First 1
    if (-not $diagram) {
        $diagram = Invoke-Api -Method POST -Path 'diagrams' -Token $leaderToken -Body @{
            projectId = $projectId; type = $sample.type; title = $sample.title
            modelJson = ($sample.model | ConvertTo-Json -Depth 30 -Compress)
        }
    }
    # Chỉ render bản Nháp: render lại cùng phiên bản đã render sẽ bị Render bỏ qua
    if ($diagram.status -eq 'Draft') {
        Invoke-Api -Method POST -Path "diagrams/$($diagram.id)/render" -Token $leaderToken | Out-Null
    }
    $current = $null
    Wait-Until {
        $script:current = Invoke-Api -Method GET -Path "diagrams/$($diagram.id)" -Token $leaderToken
        $script:current.status -in @('Rendered', 'Failed')
    } "render xong '$($sample.title)'"
    "   {0,-16} {1,-9} {2}" -f $sample.type, $current.status, $sample.title
}

@"

Xong. Đăng nhập $Leader (Leader), $($Members -join ', ') (thành viên) hoặc $Lecturer (giảng viên) để xem project '$ProjectName'.
"@
