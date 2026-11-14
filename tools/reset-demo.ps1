<#
.SYNOPSIS
  Dựng lại môi trường demo từ đầu: xóa container + volume của project umlforge-v2 → build và chạy toàn bộ
  docker compose → chờ mọi container healthy → (tùy chọn) tạo dữ liệu demo CampusEats qua Gateway.

.DESCRIPTION
  Chỉ đụng tới project "umlforge-v2" (deploy/docker-compose.yml); project v1 "umlforge" trên cùng máy không bị ảnh hưởng.
  Xóa volume là KHÔNG hoàn tác được (mất dữ liệu SQL Server + Redis của v2) → script hỏi lại trừ khi có -Force.
  -DemoData gọi API thật qua Gateway (seed-demo-data.ps1) nên dữ liệu khớp nhau ở mọi service — dùng cho buổi demo.
  -Seed chỉ bắn sự kiện mẫu với id cố định để thử consumer; id không khớp user thật nên không dùng để demo.

.EXAMPLE
  ./tools/reset-demo.ps1
  ./tools/reset-demo.ps1 -Force -DemoData
#>
param(
    # Bỏ qua câu hỏi xác nhận xóa volume
    [switch]$Force,
    # Sau khi healthy: tạo project CampusEats, thành viên, giảng viên và 5 sơ đồ đã render qua Gateway
    [switch]$DemoData,
    # Sau khi healthy: chạy seed-events.ps1 all (sự kiện mẫu để thử consumer, không phải dữ liệu demo)
    [switch]$Seed,
    [int]$TimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Text.UTF8Encoding]::new($false)
$repoRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $repoRoot 'deploy/docker-compose.yml'
$envFile = Join-Path $repoRoot 'deploy/.env'

function Invoke-Compose {
    docker compose -f $composeFile --env-file $envFile @args
    if ($LASTEXITCODE -ne 0) { throw "docker compose $($args -join ' ') thất bại (mã $LASTEXITCODE)." }
}

if (-not (Test-Path $envFile)) {
    Copy-Item (Join-Path $repoRoot 'deploy/.env.example') $envFile
    "Đã tạo deploy/.env từ .env.example (giá trị dev)."
}

if (-not $Force) {
    $answer = Read-Host 'Xóa toàn bộ container + volume của project umlforge-v2 (mất dữ liệu SQL/Redis của v2)? [y/N]'
    if ($answer -notmatch '^(y|yes|c|co|có)$') {
        'Đã hủy, không thay đổi gì.'
        return
    }
}

'== 1/3 Xóa container + volume cũ'
Invoke-Compose down --volumes --remove-orphans

'== 2/3 Build và chạy, chờ healthy (tối đa {0} s)' -f $TimeoutSeconds
Invoke-Compose up --build --detach --wait --wait-timeout $TimeoutSeconds
Invoke-Compose ps --format 'table {{.Name}}\t{{.Status}}\t{{.Ports}}'

if ($DemoData) {
    '== 3/3 Tạo dữ liệu demo CampusEats qua Gateway'
    & (Join-Path $PSScriptRoot 'seed-demo-data.ps1') -GatewayUrl 'http://localhost:5000'
}
else {
    '== 3/3 Bỏ qua dữ liệu demo (thêm -DemoData để tạo project CampusEats qua Gateway)'
}

if ($Seed) {
    '== Bắn sự kiện mẫu (thử consumer)'
    & (Join-Path $PSScriptRoot 'seed-events.ps1') all
}

@'

Môi trường demo đã sẵn sàng:
  Client (React)          http://localhost:3000
  Gateway + link Swagger  http://localhost:5000/swagger
  Swagger từng service    http://localhost:5001/swagger … http://localhost:5005/swagger
  DiagramEngine (gRPC)    127.0.0.1:5006   ·   PlantUML http://localhost:8085   ·   Redis 6379   ·   SQL Server 1433
'@
