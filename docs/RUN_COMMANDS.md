# UMLForge — lệnh chạy dự án

Dùng **PowerShell**. Mở Docker Desktop ở chế độ Linux containers trước. Tất cả lệnh dưới đây chạy từ thư mục repository:

```powershell
cd D:\FPTU\Semester8\PRN232\asm\diagramgen
```

## 1. Chuẩn bị lần đầu

Kiểm tra Docker và Compose (dự án cần Compose 2.20 trở lên):

```powershell
docker version
docker compose version
```

Tạo `.env` nếu chưa có; không ghi đè file đang dùng:

```powershell
if (!(Test-Path deploy/.env)) { Copy-Item deploy/.env.example deploy/.env }
notepad deploy/.env
```

Điền cấu hình Development theo mẫu rồi lưu. Nếu SQL đã có dữ liệu, giữ mật khẩu tương ứng với database hiện tại. Không commit `.env`.

Kiểm tra cấu hình và danh sách service mà không in giá trị secrets:

```powershell
docker compose --env-file deploy/.env -f deploy/docker-compose.yml config --quiet
docker compose --env-file deploy/.env -f deploy/docker-compose.yml config --services
```

Phải có **11 service**: `sqlserver`, `redis`, `plantuml`, `diagram-engine`, `identity`, `workspace`, `diagram`, `render`, `notification`, `gateway`, `client`.

## 2. Build và chạy toàn bộ — dùng lệnh này khi mới tải/sửa code

Ghi revision hiện tại vào các Dockerfile có hỗ trợ build arg, rồi build và chạy:

```powershell
$env:GIT_COMMIT = git rev-parse HEAD
docker compose --env-file deploy/.env -f deploy/docker-compose.yml up --build -d --wait --wait-timeout 300
docker compose --env-file deploy/.env -f deploy/docker-compose.yml ps --all
```

HEAD chỉ xác định commit nền; nếu còn sửa chưa commit thì image cũng chứa các sửa đó. Cần build lại sau khi sửa code để container dùng bản mới.

Đợi đủ **11 container chạy và healthy**. Nếu lệnh báo lỗi hoặc timeout, xem log ở mục 5; không coi chỉ có `Created` là đã chạy được.

Mở UI:

```powershell
Start-Process http://localhost:3000
```

| Thành phần | Địa chỉ local |
|---|---|
| Client | http://localhost:3000 |
| Gateway | http://localhost:5000 |
| Identity | http://localhost:5001/health |
| Workspace | http://localhost:5002/health |
| Diagram | http://localhost:5003/health |
| Render | http://localhost:5004/health |
| Notification | http://localhost:5005/health |
| PlantUML | http://localhost:8085 |
| DiagramEngine | `127.0.0.1:5006` — gRPC, không mở như trang web |
| SQL Server / Redis | `localhost:1433` / `localhost:6379` |

Đăng nhập bằng tài khoản seed Development và mật khẩu seed của cấu hình Identity. Không dùng mật khẩu SQL để đăng nhập UI.

## 3. Những lần chạy sau — không sửa code

```powershell
docker compose --env-file deploy/.env -f deploy/docker-compose.yml up -d --wait --wait-timeout 300
```

Lệnh này tạo lại container còn thiếu và chạy các container đã dừng; giữ dữ liệu trong volume.

## 4. Build riêng khi chỉ sửa một phần

Ví dụ sửa Identity:

```powershell
$env:GIT_COMMIT = git rev-parse HEAD
docker compose --env-file deploy/.env -f deploy/docker-compose.yml up --build -d --no-deps identity
```

Thay `identity` bằng `workspace`, `gateway`, `client` hoặc service cần build. `--no-deps` dùng khi các dependency như SQL/Redis đã chạy ổn.

Chỉ build image, chưa chạy container:

```powershell
docker compose --env-file deploy/.env -f deploy/docker-compose.yml build identity workspace
```

Build lại không dùng cache khi nghi cache gây vấn đề (lâu hơn):

```powershell
docker compose --env-file deploy/.env -f deploy/docker-compose.yml build --no-cache identity
docker compose --env-file deploy/.env -f deploy/docker-compose.yml up -d --no-deps identity
```

## 5. Xem trạng thái và debug

```powershell
# Container của dự án, kể cả đã dừng/chỉ Created
docker compose --env-file deploy/.env -f deploy/docker-compose.yml ps --all

# Log gần nhất của toàn hệ thống
docker compose --env-file deploy/.env -f deploy/docker-compose.yml logs --tail 100

# Theo dõi log một service; Ctrl+C chỉ thoát xem log
docker compose --env-file deploy/.env -f deploy/docker-compose.yml logs -f --tail 100 workspace

# Xem các service trên đường đi request
docker compose --env-file deploy/.env -f deploy/docker-compose.yml logs --tail 100 client gateway identity workspace

# Kiểm tra Redis
docker compose --env-file deploy/.env -f deploy/docker-compose.yml exec redis redis-cli PING

# Kiểm tra gRPC health bên trong Engine
docker compose --env-file deploy/.env -f deploy/docker-compose.yml exec diagram-engine dotnet DiagramEngine.Grpc.dll --healthcheck

# Kiểm tra Gateway từ máy
Invoke-RestMethod http://localhost:5000/health
```

Redis cần trả `PONG`. Nếu UI trả 502, kiểm tra Gateway/upstream trong log. Nếu SQL unhealthy, xem log `sqlserver` trước. Lỗi migration cần xem log service liên quan; không xóa volume để thử sửa lỗi.

Nếu cổng bị chiếm, ví dụ 3000:

```powershell
Get-NetTCPConnection -LocalPort 3000 -State Listen -ErrorAction SilentlyContinue
docker ps --format "table {{.Names}}\t{{.Ports}}"
```

## 6. Restart, dừng và chạy lại

```powershell
# Restart riêng Workspace; không build source mới
docker compose --env-file deploy/.env -f deploy/docker-compose.yml restart workspace

# Dừng cả hệ thống, giữ container và dữ liệu
docker compose --env-file deploy/.env -f deploy/docker-compose.yml stop

# Chạy lại các container đang có
docker compose --env-file deploy/.env -f deploy/docker-compose.yml start
```

Nếu đã xóa container bằng `down`, dùng `up -d` ở mục 3 thay cho `start`.

## 7. Xóa container hoặc image — giữ dữ liệu database

Dừng và xóa container/network của Compose, giữ named volumes SQL/Redis:

```powershell
docker compose --env-file deploy/.env -f deploy/docker-compose.yml down
```

Sau đó chạy lại bằng mục 2 hoặc mục 3. Nếu network còn được container khác dùng, Docker có thể giữ network; không tự xóa container của lượt kiểm khác.

Xem danh sách image và dung lượng:

```powershell
docker image ls
docker system df
```

Xóa **một image đã xác định không còn cần**, thay `IMAGE_ID` bằng ID thực tế từ danh sách:

```powershell
docker image rm IMAGE_ID
```

Nếu Docker báo image đang được container dùng, kiểm tra container đó trước; không thêm `--force`. Image đã xóa có thể build/pull lại, không phải dữ liệu SQL/Redis.

Dọn build cache không còn sử dụng; Docker sẽ hỏi xác nhận. Lần build sau có thể lâu hơn:

```powershell
docker builder prune
```

Lệnh dọn cache tác động cache Docker chung trên máy, không chỉ repository này.

## 8. Reset trắng dữ liệu — chỉ chạy khi thật sự muốn mất dữ liệu

**Lệnh sau xóa container và volume SQL/Redis của dự án: mất tài khoản, project, diagram, review, notifications và dữ liệu sự kiện hiện có.** Sao lưu dữ liệu cần giữ trước. Không dùng để restart thông thường.

```powershell
docker compose --env-file deploy/.env -f deploy/docker-compose.yml down --volumes
docker compose --env-file deploy/.env -f deploy/docker-compose.yml up --build -d --wait --wait-timeout 300
```

Service chạy migration/seed theo môi trường và cấu hình hiện tại; Development tạo dữ liệu mẫu theo seeder. Production không tự seed demo Identity.

## 9. Build/test code ngoài Docker — khi cần kiểm trước khi build image

Cần .NET SDK 8 và Node/npm phù hợp `client/package.json` trên máy:

```powershell
dotnet build src/Services/Identity/Identity.sln
dotnet test src/Services/Identity/Identity.sln

dotnet build src/Services/Workspace/Workspace.sln
dotnet test src/Services/Workspace/Workspace.sln

dotnet build src/Gateway/UMLForge.Gateway/UMLForge.Gateway.csproj

# Chạy npm ci khi chưa có node_modules hoặc lockfile thay đổi
npm --prefix client ci
npm --prefix client run check
npm --prefix client run build
```

Build/test này không cập nhật container đang chạy; muốn UI/API Docker dùng code mới thì chạy lại mục 2 hoặc mục 4.

## 10. Chạy E2E WF-01 riêng — tùy chọn

Cần Python 3, Node có WebSocket tích hợp và Edge/Chrome. Runner tự build và tạo stack riêng, thao tác browser thật, rồi dọn container/network riêng; không xóa dữ liệu stack chính:

```powershell
python -u tests/IntegrationTests/verify_workspace_flow.py --output ../.antigravity/wf01-self-test
```

Xem ảnh/log/JSON ở thư mục output. Quy trình kiểm từng bước: [SELF_TEST_WF01.md](SELF_TEST_WF01.md).

## Nhớ nhanh

| Muốn làm gì | Lệnh Compose chính (giữ nguyên `--env-file` và `-f` như các ví dụ trên) |
|---|---|
| Build và chạy code mới | `up --build -d --wait --wait-timeout 300` |
| Chạy lại hằng ngày | `up -d --wait --wait-timeout 300` |
| Xem trạng thái | `ps --all` |
| Theo dõi lỗi | `logs -f --tail 100 SERVICE` |
| Restart một service | `restart SERVICE` |
| Dừng, giữ container | `stop` |
| Xóa container, giữ dữ liệu | `down` |
| Reset mất dữ liệu | `down --volumes` — đọc mục 8 trước |
