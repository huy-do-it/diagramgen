# UMLForge v2

Nền tảng giúp nhóm sinh viên capstone tạo, kiểm tra luật UML, render và duyệt bộ 5 sơ đồ (Business Context, Use Case,
Sequence, Activity, State). Môn PRN232 — nhóm Huy, Thiện, Minh, Bảo.

Kiến trúc **microservices + database-per-service + event-driven (Redis Streams)**, .NET 8, React 18.
Chi tiết: [PRD](docs/PRD.md) · [ARCHITECTURE](docs/ARCHITECTURE.md) · [DESIGN](docs/DESIGN.md) ·
[RULES](docs/RULES.md) · [TASKS](docs/TASKS.md) (hợp đồng chung ở mục 1) · [PROGRESS](docs/PROGRESS.md).

```
React SPA (nginx :3000) ──▶ Gateway YARP :5000 ──▶ Identity · Workspace · Diagram · Render · Notification
                                                        │ Outbox → Redis Streams → consumer (Inbox)
                                       Diagram, Render ──gRPC──▶ DiagramEngine ── Render ──HTTP──▶ PlantUML
SQL Server: IdentityDb · WorkspaceDb · DiagramDb · RenderDb · NotificationDb
```

## Service và cổng

| Service | Cổng máy | Người phụ trách | Ghi chú |
|---|---|---|---|
| client (React + nginx) | 3000 | Bảo | `/api/*` → gateway |
| gateway (YARP) | 5000 | Huy | JWT, CORS, rate limit, link Swagger |
| identity | 5001 | Huy | IdentityDb |
| workspace | 5002 | Huy | WorkspaceDb |
| diagram | 5003 | Thiện | DiagramDb |
| render | 5004 | Minh | RenderDb, job định kỳ |
| notification | 5005 | Minh | NotificationDb |
| diagram-engine (gRPC) | 127.0.0.1:5006 | Bảo | chỉ nội bộ; h2c |
| sqlserver · redis · plantuml | 1433 · 6379 · 8085 | Bảo | hạ tầng |

## Yêu cầu

.NET SDK 8 (đã ghim bằng `global.json`) · Node 20.19+ / 22 · Docker Desktop (Compose ≥ 2.20, RAM ~4–5 GB).

## Chạy toàn bộ bằng Docker

```bash
cp deploy/.env.example deploy/.env          # giá trị dev, không dùng cho môi trường thật
docker compose -f deploy/docker-compose.yml up --build -d --wait
```

Mở http://localhost:3000. Dừng: `docker compose -f deploy/docker-compose.yml down` (thêm `-v` để xóa dữ liệu).
Project Compose tên **`umlforge-v2`** để không trùng container/volume với v1 trên cùng máy.

Dựng lại từ đầu (xóa volume, build, chờ healthy, bắn sự kiện mẫu):

```powershell
./tools/reset-demo.ps1 -Seed
```

## Chạy 1 service trên máy (dev)

```bash
docker compose -p umlforge-v2 -f deploy/compose/infra.yml --env-file deploy/.env up -d
```

```bash
dotnet run --project src/Services/Diagram/Diagram.API
```

Cấu hình khi chạy trên máy: `ConnectionStrings:Redis = localhost:6379`, SQL `Server=localhost,1433`,
`DiagramEngine:Url = http://localhost:5006` (chạy engine: `docker compose -f deploy/docker-compose.yml up -d diagram-engine`).

Client: `cd client && npm ci && npm run dev` → http://127.0.0.1:5173. Vite proxy thẳng `/api/*` tới cổng 5001–5005;
khi đã có Gateway đặt `VITE_USE_GATEWAY=true` trong `client/.env.local`.

## Công cụ test (thư mục `tools/`)

| Script | Dùng để |
|---|---|
| `make-dev-token.ps1 -Role Lecturer -Name lecturer1 [-UserId <guid>]` | Tạo JWT dev đúng claim (TASKS §1.2) → dán vào nút **Authorize** của Swagger |
| `seed-events.ps1 <identity\|workspace\|diagram\|render\|all> [-NewIds]` | Bắn sự kiện mẫu CampusEats (`tools/events/*/*.json`) lên Redis Streams. Chạy lại cùng id → consumer bỏ qua (Inbox); `-NewIds` để xử lý lại |
| `reset-demo.ps1 [-Force] [-Seed]` | Xóa container + volume của `umlforge-v2`, build, chờ healthy, (tùy chọn) bắn sự kiện mẫu |

Id cố định trong sự kiện mẫu (tạo token đúng người bằng `-UserId`): admin `a0000000-0000-0000-0000-000000000001`,
lecturer1 `b0000000-…-000000000001`, student1…6 `c0000000-…-00000000000[1-6]`, project CampusEats
`d0000000-0000-0000-0000-000000000001`. Xem sự kiện: `docker exec -it umlforge-redis redis-cli XRANGE workspace.events - +`.

## Tài khoản demo

Identity seed tài khoản `admin`, `lecturer1`, `lecturer2`, `student1…4` khi chạy môi trường Development; mật khẩu nằm
trong `src/Services/Identity/Identity.API/appsettings.Development.json` (không ghi trong code hay README).

## Swagger và job chạy tay

- Swagger: http://localhost:5001/swagger … http://localhost:5005/swagger, trang tổng ở http://localhost:5000/swagger.
- Job định kỳ (chỉ Admin, token từ `make-dev-token.ps1 -Role Admin`):
  - Danh sách: `GET /api/admin/{service}/jobs`
  - Chạy ngay: `POST /api/admin/{service}/jobs/{jobName}/run` → 202 kèm kết quả; log dạng `[job-name] tóm tắt in N ms`.
- DiagramEngine: gRPC `diagramengine.DiagramEngineService` tại `127.0.0.1:5006` (Postman → gRPC request, reflection
  bật ở Development).

## Test và CI

```bash
dotnet test src/BuildingBlocks/BuildingBlocks.sln
```

Integration test dùng **Testcontainers** (Redis + SQL Server thật) → cần Docker đang chạy. GitHub Actions
(`.github/workflows/ci.yml`) tự tìm mọi `src/**/*.sln`: build `-warnaserror` + test, build client, kiểm tra
`docker compose config`. Không merge khi CI đỏ.

## Cấu trúc và quy ước

Mỗi service có `.sln`, database, file `deploy/compose/<service>.yml` riêng; mỗi người chỉ sửa thư mục của mình
(TASKS §0.1). Gói dùng chung trong `src/BuildingBlocks` (Web/Auth/Outbox/EventBus/Jobs) và `src/Contracts`
(16 sự kiện, proto gRPC). Lưu ý kỹ thuật cho cả nhóm: [PROGRESS §2.2](docs/PROGRESS.md).
