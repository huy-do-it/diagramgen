<#
.SYNOPSIS
  Bắn sự kiện mẫu (tools/events/<stream>/*.json) lên Redis Streams để test consumer khi chưa có service phát (TASKS §8.2).

.DESCRIPTION
  Mỗi file là 1 envelope đầy đủ (TASKS §1.4) với id cố định dễ nhớ:
    admin      a0000000-0000-0000-0000-000000000001
    lecturer1  b0000000-0000-0000-0000-000000000001   lecturer2  b0000000-...-000000000002
    student1…6 c0000000-0000-0000-0000-00000000000[1-6]
    project CampusEats d0000000-0000-0000-0000-000000000001
    diagram  Use Case  f0000000-0000-0000-0000-000000000001   Sequence f0000000-...-000000000002
  Tạo token đúng người: ./tools/make-dev-token.ps1 -UserId c0000000-0000-0000-0000-000000000001 -Name student1

  Chạy lại với eventId cũ → consumer bỏ qua nhờ Inbox (kiểm tra idempotent). Thêm -NewIds để consumer xử lý lại.

.EXAMPLE
  ./tools/seed-events.ps1 identity
  ./tools/seed-events.ps1 all -NewIds
  docker exec -it umlforge-redis redis-cli XRANGE workspace.events - +
#>
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('identity', 'workspace', 'diagram', 'render', 'all')]
    [string]$Stream,
    [switch]$NewIds,
    [string]$Container = 'umlforge-redis'
)

$ErrorActionPreference = 'Stop'
# Gửi UTF-8 vào stdin của docker (Windows PowerShell 5.1 mặc định ASCII → mất dấu tiếng Việt)
$OutputEncoding = [Text.UTF8Encoding]::new($false)

if ((docker exec $Container redis-cli PING 2>$null) -ne 'PONG') {
    throw "Không kết nối được Redis trong container '$Container'. Chạy hạ tầng trước: docker compose -p umlforge-v2 -f deploy/compose/infra.yml --env-file deploy/.env up -d"
}

$streams = if ($Stream -eq 'all') { @('identity', 'workspace', 'diagram', 'render') } else { @($Stream) }
$typePattern = [regex]'"type"\s*:\s*"(?<type>\w+)"'
$eventIdPattern = [regex]'"eventId"\s*:\s*"[0-9a-fA-F-]+"'
$sent = 0

foreach ($name in $streams) {
    $streamName = "$name.events"
    $files = Get-ChildItem -Path (Join-Path $PSScriptRoot "events/$name") -Filter '*.json' | Sort-Object Name
    foreach ($file in $files) {
        $envelope = Get-Content -Path $file.FullName -Raw -Encoding UTF8
        # "type" đầu tiên là của envelope (đứng trước payload)
        $type = $typePattern.Match($envelope).Groups['type'].Value
        if (-not $type) { throw "File $($file.Name) thiếu trường 'type'." }
        if ($NewIds) {
            $envelope = $eventIdPattern.Replace($envelope, "`"eventId`": `"$([guid]::NewGuid())`"", 1)
        }

        # -x: redis-cli lấy tham số cuối (envelope) từ stdin → không bị shell làm hỏng dấu ngoặc kép
        $id = $envelope | docker exec -i $Container redis-cli -x XADD $streamName MAXLEN '~' 10000 '*' type $type envelope
        if ($LASTEXITCODE -ne 0 -or -not $id) { throw "XADD $streamName thất bại với file $($file.Name)." }
        "→ {0,-17} {1,-17} {2}  ({3})" -f $streamName, $type, $id, $file.Name
        $sent++
    }
}

""
"Đã gửi $sent sự kiện. Xem: docker exec -it $Container redis-cli XRANGE <stream> - +   ·   XINFO GROUPS <stream>"
