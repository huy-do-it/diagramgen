<#
.SYNOPSIS
    Kiểm tra sức khỏe toàn diện hệ thống UMLForge v2.
.DESCRIPTION
    Script thực hiện ping TCP, kiểm tra Docker Container, ping SQL/Redis và gọi HTTP /health.
#>

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   UMLFORGE V2 — HEALTH CHECK & CONNECTIVITY AUDIT       " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$services = @(
    @{ Name = "API Gateway";       Port = 5000; Path = "/swagger"; Expected = 200 },
    @{ Name = "Identity Service";   Port = 5001; Path = "/health";  Expected = 200 },
    @{ Name = "Workspace Service";  Port = 5002; Path = "/health";  Expected = 200 },
    @{ Name = "Diagram Service";    Port = 5003; Path = "/health";  Expected = 200 },
    @{ Name = "Render Worker";      Port = 5004; Path = "/health";  Expected = 200 },
    @{ Name = "Notification Svc";   Port = 5005; Path = "/health";  Expected = 200 },
    @{ Name = "PlantUML Server";    Port = 8085; Path = "/";        Expected = 200 },
    @{ Name = "Client Web SPA";     Port = 3000; Path = "/";        Expected = 200 }
)

Write-Host "`n[1/3] KIỂM TRA HTTP ENDPOINTS..." -ForegroundColor Yellow
foreach ($svc in $services) {
    $url = "http://localhost:$($svc.Port)$($svc.Path)"
    try {
        $response = Invoke-WebRequest -Uri $url -Method Get -TimeoutSec 3 -UseBasicParsing -ErrorAction Stop
        if ($response.StatusCode -eq $svc.Expected) {
            Write-Host "  [OK] $($svc.Name) ($url) - HTTP $($response.StatusCode)" -ForegroundColor Green
        } else {
            Write-Host "  [WARN] $($svc.Name) ($url) - Trả về HTTP $($response.StatusCode)" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "  [FAIL] $($svc.Name) ($url) - Không thể kết nối ($($_.Exception.Message))" -ForegroundColor Red
    }
}

Write-Host "`n[2/3] KIỂM TRA HẠ TẦNG CƠ SỞ DỮ LIỆU & BROKER..." -ForegroundColor Yellow

# Kiểm tra Redis Ping
try {
    $redisPing = docker exec umlforge-redis redis-cli ping 2>$null
    if ($redisPing -match "PONG") {
        Write-Host "  [OK] Redis 7 Streams: PONG (Sẵn sàng)" -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] Redis 7 Streams: Không phản hồi" -ForegroundColor Red
    }
} catch {
    Write-Host "  [FAIL] Redis 7 Streams: Container chưa chạy" -ForegroundColor Red
}

# Kiểm tra SQL Server Ping
try {
    # Mật khẩu đọc từ biến môi trường MSSQL_SA_PASSWORD bên trong container — không ghi secret trong script
    $sqlPing = docker exec umlforge-sqlserver bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "SELECT @@VERSION;"' 2>$null
    if ($sqlPing -match "Microsoft SQL Server") {
        Write-Host "  [OK] SQL Server 2022: Hoạt động bình thường" -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] SQL Server 2022: Lỗi xác thực hoặc database offline" -ForegroundColor Red
    }
} catch {
    Write-Host "  [FAIL] SQL Server 2022: Container chưa chạy" -ForegroundColor Red
}

Write-Host "`n[3/3] TRẠNG THÁI CONTAINER DOCKER..." -ForegroundColor Yellow
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}" | Select-String "umlforge"

Write-Host "`n>> QUÁ TRÌNH KIỂM TRA HOÀN TẤT." -ForegroundColor Cyan

