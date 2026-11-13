<#
.SYNOPSIS
    Gom và hiển thị luồng log thời gian thực của toàn bộ hệ thống UMLForge v2.
.EXAMPLE
    ./tools/tail-all-logs.ps1
    ./tools/tail-all-logs.ps1 -Service diagram, render -Tail 50
#>
param(
    # Bỏ trống = mọi service đang có trong compose (tự nhận service mới, không lỗi khi gateway chưa có)
    [string[]]$Service = @(),
    [int]$Tail = 10
)

Write-Host "Đang gắn kết luồng Log toàn hệ thống UMLForge v2 (Bấm Ctrl+C để dừng)..." -ForegroundColor Cyan

$composeFile = Join-Path $PSScriptRoot '..\deploy\docker-compose.yml'
docker compose -f $composeFile logs -f --tail $Tail @Service

