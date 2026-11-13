<#
.SYNOPSIS
  Dựng lại môi trường demo từ đầu: xóa container + volume của project umlforge-v2 → build và chạy toàn bộ
  docker compose → chờ mọi container healthy → (tùy chọn) bắn sự kiện mẫu CampusEats.

.DESCRIPTION
  Chỉ đụng tới project "umlforge-v2" (deploy/docker-compose.yml); project v1 "umlforge" trên cùng máy không bị ảnh hưởng.
  Xóa volume là KHÔNG hoàn tác được (mất dữ liệu SQL Server + Redis của v2) → script hỏi lại trừ khi có -Force.

.EXAMPLE
  ./tools/reset-demo.ps1
  ./tools/reset-demo.ps1 -Force -Seed
#>
param(
    # Bỏ qua câu hỏi xác nhận xóa volume
    [switch]$Force,
    # Sau khi healthy: chạy seed-events.ps1 all (sự kiện mẫu CampusEats)
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

if ($Seed) {
    '== 3/3 Bắn sự kiện mẫu CampusEats'
    & (Join-Path $PSScriptRoot 'seed-events.ps1') all
}
else {
    '== 3/3 Bỏ qua sự kiện mẫu (thêm -Seed để bắn)'
}

@'

Môi trường demo đã sẵn sàng:
  Client (React)          http://localhost:3000
  Gateway + link Swagger  http://localhost:5000/swagger   (khi đã có gateway)
  Swagger từng service    http://localhost:5001/swagger … http://localhost:5005/swagger
  DiagramEngine (gRPC)    127.0.0.1:5006   ·   PlantUML http://localhost:8085   ·   Redis 6379   ·   SQL Server 1433
'@
