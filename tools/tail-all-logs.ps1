<#
.SYNOPSIS
    Gom và hiển thị luồng log thời gian thực của toàn bộ hệ thống UMLForge v2.
#>

Write-Host "Đang gắn kết luồng Log toàn hệ thống UMLForge v2 (Bấm Ctrl+C để dừng)..." -ForegroundColor Cyan

# Kiểm tra docker compose command
$composeFile = Join-Path $PSScriptRoot "..\deploy\docker-compose.yml"
docker compose -f $composeFile logs -f --tail 10 gateway identity workspace diagram render notification engine

