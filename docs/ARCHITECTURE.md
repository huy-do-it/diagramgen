# ARCHITECTURE — UMLForge v2

> Công nghệ sử dụng, cấu trúc thư mục, các service, luồng dữ liệu, sự kiện, gRPC, job nền và cách triển khai.
> Kiểu kiến trúc: **Microservices + Database-per-Service + Event-driven (Redis Streams)**.

---

## 1. Technology Stack

| Nhóm | Công nghệ | Dùng để làm gì |
|---|---|---|
| Backend | **.NET 8**, ASP.NET Core Web API | 5 service REST, theo 3 lớp API → Service → Repository |
| Gateway | **YARP** (Yet Another Reverse Proxy) | Cổng vào duy nhất, định tuyến, kiểm tra JWT, rate limit, CORS |
| Data | **EF Core 8** + **SQL Server 2022** | 1 instance, **5 database riêng** (mỗi service 1 DB), migration riêng |
| Broker | **Redis 7 Streams** (StackExchange.Redis) | Sự kiện nghiệp vụ bền vững giữa các service (consumer group) |
| RPC | **gRPC** (Grpc.AspNetCore, Grpc.Net.ClientFactory) | Diagram Engine: kiểm tra luật UML, sinh PlantUML |
| Background | **.NET BackgroundService** + `PeriodicTimer` (giống v1) | Consumer/outbox chạy liên tục; job định kỳ (báo cáo, dọn dẹp, reconcile) |
| Auth | **JWT Bearer** (HS256) + refresh token, **BCrypt** | Xác thực, phân quyền theo vai trò |
| Validation | **FluentValidation** | Kiểm tra request DTO |
| Render | **PlantUML Server** (`plantuml/plantuml-server:jetty`) | Mã PlantUML → SVG |
| Frontend | **React 18 + TypeScript + Vite**, served bởi **nginx** | SPA cho Student / Lecturer / Admin |
| Cập nhật trạng thái | **Polling** bằng hook tự viết (`setTimeout` 2 s, kế thừa `useJobProgress.ts` của v1 — không thêm thư viện) | Client tự hỏi lại trạng thái render và số thông báo chưa đọc |
| Test | **xUnit**, FluentAssertions, Moq, **Testcontainers** | Unit test theo BR, integration test với SQL/Redis thật |
| DevOps | **Docker**, **Docker Compose**, **GitHub Actions** | Đóng gói, chạy 1 lệnh, CI build + test |
| API docs | **Swashbuckle (Swagger / OpenAPI 3)** | Mỗi service có `/swagger`, Gateway gộp link |

---

## 2. Danh sách service

| Service | Loại | Port (host) | Database | Trách nhiệm |
|---|---|---|---|---|
| `gateway` | YARP | **5000** | — | Định tuyến, JWT, rate limit, CORS, CorrelationId |
| `identity` | REST | 5001 | IdentityDb | Tài khoản, JWT, refresh token, quản lý user |
| `workspace` | REST | 5002 | WorkspaceDb | Project, thành viên, lời mời, gán giảng viên |
| `diagram` | REST | 5003 | DiagramDb | Diagram, phiên bản, render request, review, comment, export |
| `render` | Worker + REST nhỏ | 5004 | RenderDb | Consumer render, artifact, báo cáo, reconcile, API admin chạy job thủ công |
| `notification` | REST + Worker | 5005 | NotificationDb | Thông báo trong app (tạo từ sự kiện, đọc, đếm chưa đọc) |
| `diagram-engine` | **gRPC** | 5006 (chỉ nội bộ) | — | Validate, Suggest, GeneratePlantUml, ValidateProject |
| `client` | nginx | **3000** | — | React SPA |
| `sqlserver` | infra | 1433 | 5 DB | |
| `redis` | infra | 6379 | — | Redis Streams (AOF bật để không mất sự kiện) |
| `plantuml` | infra | 8085 | — | Render SVG |

**Tổng: 11 container** (7 của nhóm + client + 3 hạ tầng).

---

## 3. Sơ đồ kiến trúc (C4 – Container)

```
                         React SPA (nginx :3000)
                                  │ HTTPS + JWT
                        ┌─────────▼──────────┐
                        │ API Gateway (YARP) │ :5000
                        └─┬──────┬──────┬──┬─┘
             ┌────────────┘      │      │  └──────────────┐
      ┌──────▼─────┐   ┌─────────▼──┐ ┌─▼────────────┐ ┌──▼──────────────┐
      │ Identity   │   │ Workspace  │ │ Diagram      │ │ Notification    │
      │ IdentityDb │   │ WorkspaceDb│ │ DiagramDb    │ │ NotificationDb  │
      └─────┬──────┘   └─────┬──────┘ └──┬───────▲───┘ └────────▲────────┘
            │ events         │ events    │ gRPC  │ events       │ events (chỉ nhận)
            ▼                ▼           ▼       │              │
      ═════════════════ Redis Streams (event bus) ═════════════════
                                  │ RenderRequested    ▲ RenderCompleted/Failed
                          ┌───────▼────────────────────┴─┐
                          │ Render (Worker)              │──gRPC──▶ DiagramEngine (stateless)
                          │ RenderDb                     │──HTTP──▶ PlantUML Server
                          └──────────────────────────────┘
       SQL Server: IdentityDb · WorkspaceDb · DiagramDb · RenderDb · NotificationDb
```

### Quy tắc giao tiếp
| Kiểu | Khi nào | Công nghệ |
|---|---|---|
| Client → hệ thống | Mọi thao tác người dùng | REST qua Gateway |
| Service → service, **cần kết quả ngay** | Validate khi lưu, sinh PlantUML khi render | **gRPC** |
| Service → service, **không cần chờ** | Mọi thay đổi nghiệp vụ cần báo cho service khác | **Redis Streams** (qua Outbox) |
| Trình duyệt theo dõi trạng thái | Render xong chưa, có thông báo mới không | **Polling** REST mỗi 2 s (dừng khi trạng thái đã kết thúc) |

> Không dùng Redis Pub/Sub / SSE / SignalR: không phải tiêu chí chấm, polling đủ cho quy mô đồ án.

**Cấm:** service gọi REST trực tiếp sang service khác · service đọc DB của service khác · khóa ngoại xuyên database.

---

## 4. Cấu trúc thư mục

```
umlforge/
├── src/
│   ├── Directory.Build.props
│   ├── BuildingBlocks/                              # Bảo
│   │   ├── UMLForge.BuildingBlocks.EventBus/        # EventConsumer (XREADGROUP/XACK), IEventHandler<T>, IStreamMaintenance
│   │   ├── UMLForge.BuildingBlocks.Outbox/          # OutboxMessage, InboxMessage, IOutbox, OutboxPublisher (BackgroundService)
│   │   ├── UMLForge.BuildingBlocks.Jobs/            # ScheduledJobBase (PeriodicTimer), IJobTrigger, MapJobTriggerEndpoint
│   │   ├── UMLForge.BuildingBlocks.Auth/            # AddUmlForgeJwt(), ICurrentUser
│   │   └── UMLForge.BuildingBlocks.Web/             # ProblemDetails, CorrelationId, Serilog, Swagger, /health, PagedResult
│   ├── Contracts/                                   # Bảo
│   │   ├── UMLForge.Contracts.Events/               # EventEnvelope, StreamNames, 16 record sự kiện
│   │   └── UMLForge.Contracts.Grpc/                 # diagram_engine.proto (GrpcServices="Both")
│   ├── Gateway/UMLForge.Gateway/                    # Huy — YARP
│   └── Services/                                    # mỗi service 1 file .sln riêng + thư mục tests riêng
│       ├── Identity/      Identity.sln · Identity.API · Identity.Service · Identity.Repositories · Identity.Tests        # Huy
│       ├── Workspace/     Workspace.sln · Workspace.API · .Service · .Repositories · .Tests                          # Huy
│       ├── Diagram/       Diagram.sln · Diagram.API · .Service · .Repositories · .Tests                              # Thiện
│       ├── Render/        Render.sln · Render.Worker · .Service · .Repositories · .Tests                             # Minh
│       ├── Notification/  Notification.sln · Notification.API · .Service · .Repositories · .Tests                    # Minh
│       └── DiagramEngine/ DiagramEngine.sln · DiagramEngine.Grpc (Generators/, Validators/) · DiagramEngine.Tests      # Bảo
├── client/                                          # React + Vite (+ Dockerfile nginx)
│   └── src/
│       ├── features/{identity,workspace,diagram,editor,render,notification}/   # mỗi người 1 thư mục + routes.tsx
│       └── shared/                                  # api, hook dùng chung (useRenderStatus…)
├── tests/IntegrationTests/                          # Testcontainers: WF-01…03
├── deploy/
│   ├── docker-compose.yml                           # chỉ "include" các file bên dưới
│   ├── compose/{infra,engine,identity,workspace,gateway,diagram,render,notification,client}.yml
│   └── .env.example
├── tools/                                           # make-dev-token.ps1, seed-events.ps1, events/*.json, reset-demo.ps1
├── docs/                                            # PRD, ARCHITECTURE, DESIGN, RULES, TASKS, PROGRESS
└── .github/workflows/ci.yml                         # build + test theo từng .sln
```

---

## 5. Database (mỗi service một DB)

```
IdentityDb
  Users(Id, Username UQ, PasswordHash, FullName, Role, IsLocked, CreatedAt)
  RefreshTokens(Id, UserId, TokenHash UQ, ExpiresAt, RevokedAt)
  OutboxMessages

WorkspaceDb
  Projects(Id, Name, Description, OwnerId, LecturerId?, CreatedAt, RowVersion)
  ProjectMembers(ProjectId, UserId, Role[Leader|Member], JoinedAt)         PK(ProjectId, UserId)
  Invitations(Id, ProjectId, InviteeId, Status, ExpiresAt)
  UserDirectory(UserId, Username, FullName, Role)          ← bản sao từ UserRegistered
  OutboxMessages, InboxMessages

DiagramDb
  Diagrams(Id, ProjectId, Type, Title, ModelJson, Status, CurrentVersion, SvgXml?, RenderedAt?, IsDeleted, RowVersion)
  DiagramVersions(Id, DiagramId, Version, ModelJson, CreatedBy, CreatedAt)   UQ(DiagramId, Version)
  Reviews(Id, ProjectId, Status, SubmittedBy, DecidedBy?, Snapshot JSON)
  Comments(Id, DiagramId, AuthorId, Content, CreatedAt)
  MemberAccess(ProjectId, UserId, Role[Leader|Member|Lecturer])   ← bản sao từ sự kiện Workspace
  OutboxMessages, InboxMessages

RenderDb
  RenderJobs(Id, DiagramId, ProjectId, DiagramVersion, DiagramType, Title, ModelJson, RequestedBy, Status, Attempt, Error?, CreatedAt, StartedAt?, FinishedAt?, DurationMs?)  UQ(DiagramId, DiagramVersion)
  RenderArtifacts(JobId, PlantUml, SvgXml)
  RenderDailyReports(Date PK, Total, Done, Failed, AvgMs, SuccessRate)
  OutboxMessages, InboxMessages

NotificationDb
  Notifications(Id, UserId, Type, Title, Link, IsRead, ReadAt?, CreatedAt)   IX(UserId, IsRead)
  ProjectRecipients(ProjectId, UserId, Role)   ← bản sao từ sự kiện Workspace, để biết gửi thông báo cho ai (BR-34)
  InboxMessages
```

---

## 6. Event Bus (Redis Streams)

### 6.1 Envelope chung
```json
{
  "eventId": "8f0c…", "type": "RenderRequested", "version": 1,
  "occurredAt": "2026-11-12T08:00:00Z", "correlationId": "c1a2…",
  "producer": "diagram",
  "payload": { }
}
```

### 6.2 Danh mục sự kiện
| Stream | Sự kiện | Producer | Consumer group |
|---|---|---|---|
| `identity.events` | `UserRegistered`, `UserRoleChanged`, `UserLocked` | identity | `workspace-svc`, `notification-svc` |
| `workspace.events` | `ProjectCreated`, `InvitationSent`, `MemberAdded`, `MemberRemoved`, `LecturerAssigned`, `ProjectDeleted` | workspace | `diagram-svc`, `notification-svc` |
| `diagram.events` | `RenderRequested`, `ReviewSubmitted`, `ReviewDecided`, `CommentAdded` | diagram | `render-workers`, `notification-svc` |
| `render.events` | `RenderStarted`, `RenderCompleted`, `RenderFailed` | render | `diagram-svc`, `notification-svc` |

> Mọi sự kiện thuộc một project đều mang `projectId` và `actorUserId` trong payload để Notification tìm người nhận (`ProjectRecipients`) và bỏ qua chính người thực hiện (BR-34).

> Vì sao Streams mà không phải List (v1) hay Pub/Sub: List chỉ cho **1** consumer nhận mỗi message; Pub/Sub gửi xong là quên, consumer tắt thì **mất** message. Streams có consumer group (mỗi service nhận đủ), lưu bền, có `XACK` và xử lý lại message chưa ack.

### 6.3 Đảm bảo giao nhận
- **Outbox:** lưu nghiệp vụ + `OutboxMessages` trong 1 transaction → `OutboxPublisher` (1–2 s) `XADD … MAXLEN ~ 10000`.
- **Consumer:** `XREADGROUP GROUP <group> <consumer> COUNT 10 STREAMS <stream> >` (poll 200 ms) → xử lý → `XACK`.
- **Inbox:** bỏ qua `eventId` đã xử lý (idempotent).
- **Retry/DLQ:** `ReconcileJob` (BackgroundService, 2 phút/lần) đọc `XPENDING`; `deliveryCount ≥ 5` → `XADD <stream>.dlq` + `XACK`; còn lại `XCLAIM` sau 60 s idle.
- **Ngữ nghĩa:** at-least-once + idempotent consumer = xử lý đúng một lần về mặt nghiệp vụ.

---

## 7. gRPC — DiagramEngine

```proto
service DiagramEngine {
  rpc ValidateDiagram  (DiagramRequest)  returns (ValidationResult);          // unary
  rpc SuggestRelations (DiagramRequest)  returns (SuggestionList);            // unary
  rpc GeneratePlantUml (DiagramRequest)  returns (PlantUmlResult);            // unary
  rpc ValidateProject  (ProjectRequest)  returns (stream DiagramValidation);  // server streaming
}
```

| Thiết lập | Giá trị |
|---|---|
| Caller | `diagram` (Validate, Suggest, ValidateProject), `render` (Validate, GeneratePlantUml) |
| Deadline | 5 s mỗi lần gọi |
| Retry | tối đa 2 lần, chỉ khi `Unavailable` |
| Health | `grpc.health.v1.Health` — compose dùng làm healthcheck |
| Interceptor | Logging, truyền `x-correlation-id`, đo thời gian |
| Bảo mật | Chỉ trong mạng Docker nội bộ, không qua Gateway |

---

## 8. Background Jobs

Chỉ dùng **.NET BackgroundService** — giống cách nhóm đang làm ở v1 (`WorkerService`, `CleanupHostedService`, `ReconcileHostedService`). Không dùng Hangfire.

**Hai loại job:**

| Loại | Cách viết | Đáp ứng yêu cầu đề |
|---|---|---|
| Chạy liên tục (consumer, outbox) | `BackgroundService` + vòng `while (!stoppingToken.IsCancellationRequested)` | Xử lý tác vụ bất đồng bộ |
| Định kỳ | Lớp nền `ScheduledJobBase` (BuildingBlocks) = `BackgroundService` + `PeriodicTimer` + cờ `RunOnStartup` | Tác vụ định kỳ |

| Job | Service | Loại | Chu kỳ |
|---|---|---|---|
| `RenderRequestedConsumer` | render | Liên tục | poll 200 ms |
| `WorkspaceEventsConsumer`, `RenderEventsConsumer` | diagram | Liên tục | poll 200 ms |
| `IdentityEventsConsumer` | workspace | Liên tục | poll 200 ms |
| `NotificationEventsConsumer` (mọi stream) | notification | Liên tục | poll 200 ms |
| `OutboxPublisher` | identity, workspace, diagram, render | Liên tục | 1 s |
| `ReconcileJob` (XPENDING/XCLAIM, job kẹt > 5 phút, DLQ) | render | Định kỳ | 2 phút |
| `CleanupJob` (job lỗi > 7 ngày, artifact mồ côi, Inbox/Outbox cũ) | render | Định kỳ | 1 giờ |
| `DailyRenderReportJob` | render | Định kỳ | 1 giờ, chỉ chạy khi chưa có báo cáo của ngày hôm qua |
| `ExpireInvitationsJob` (BR-09) | workspace | Định kỳ | 1 giờ |
| `PurgeRefreshTokensJob` | identity | Định kỳ | 6 giờ |
| `PurgeReadNotificationsJob` (BR-35) | notification | Định kỳ | 6 giờ |

**Chạy thủ công khi demo** (thay cho dashboard): mỗi service có
`POST /api/admin/jobs/{jobName}/run` (chỉ Admin) → gọi `IJobTrigger.RunNowAsync(jobName)`, trả 202 kèm kết quả tóm tắt trong log.
Job định kỳ ghi log dạng `[CleanupJob] removed 3 failed jobs, 1 orphan artifact in 42 ms` để chứng minh khi thuyết trình.

---

## 9. Luồng dữ liệu tiêu biểu — Render (WF-02)

```
1. Browser  ── POST /api/diagrams/12/render (JWT) ──▶ Gateway ──▶ Diagram.API
2. Diagram  ── kiểm tra MemberAccess (BR-11), trạng thái (BR-14)
            ── 1 transaction: Diagrams.Status = Pending + OutboxMessages(RenderRequested{v3, modelJson})
            ◀── 202 Accepted { diagramId, version: 3 }
3. OutboxPublisher (diagram) ── XADD diagram.events
4. Render  ── XREADGROUP render-workers ── Inbox check (BR-26) ── RenderJobs(Queued→Running)
           ── Outbox(RenderStarted) → Diagram đổi Status = Rendering
           ── gRPC ValidateDiagram → GeneratePlantUml ──▶ DiagramEngine
           ── HTTP POST /svg ──▶ PlantUML   (timeout 30 s, BR-27)
           ── RenderArtifacts + Outbox(RenderCompleted{svg}) ── XACK
5. Diagram ── consume RenderCompleted ── Diagrams.SvgXml, Status = Rendered
   Notification ── consume ── tạo Notification "Render xong" cho thành viên project (BR-34)
6. Browser ── GET /api/diagrams/12 mỗi 2 s khi Status ∈ {Pending, Rendering} → thấy Rendered thì dừng
           ── GET /api/notifications/unread-count mỗi 30 s → chuông 🔔 tăng số
```

---

## 10. Triển khai (Docker Compose)

| Thứ tự khởi động | Service | Điều kiện |
|---|---|---|
| 1 | sqlserver, redis, plantuml | — |
| 2 | diagram-engine | — (healthcheck gRPC) |
| 3 | identity, workspace, diagram, render, notification | sqlserver + redis healthy; render/diagram chờ diagram-engine |
| 4 | gateway | 5 service healthy |
| 5 | client | gateway healthy |

- Mỗi service tự chạy `Database.MigrateAsync()` cho DB của mình khi khởi động. Identity và Workspace áp dụng migration cho relational DB ở mọi môi trường, trước khi host nhận request; lỗi khởi tạo schema phải log critical và dừng startup, không tiếp tục như service khỏe.
- Identity chỉ seed tài khoản mẫu Admin/Lecturer/Student trong **Development**. Production chạy migration nhưng không tự tạo tài khoản demo; phần kiểm Production dưới đây là schema/startup, không thay thế quy trình cấp tài khoản quản trị Production được phê duyệt. WF nghiệm thu có tài khoản mẫu dùng Development.
- Biến môi trường dùng chung trong `.env`: `MSSQL_SA_PASSWORD`, `JWT_KEY`, `JWT_ISSUER`, `REDIS_URL`.
- Chạy: `docker compose -f deploy/docker-compose.yml up --build -d` → mở `http://localhost:3000`.
- Mỗi service có file `deploy/compose/<service>.yml` riêng; file gốc dùng `include` (cần Docker Compose ≥ 2.20) → mỗi người sửa file của mình, không xung đột.
- Dockerfile build với context `src/` để copy được `Contracts/` và `BuildingBlocks/`.

> Hợp đồng chi tiết (route, JWT claim, payload sự kiện, chữ ký interface chung) nằm ở **TASKS.md mục 1** — đó là nguồn chính thức khi code.
- Ước tính tài nguyên: ~4–5 GB RAM (SQL Server ~1.5 GB).

---

## 11. Quyết định kiến trúc (ADR tóm tắt)

| # | Quyết định | Lý do | Đánh đổi |
|---|---|---|---|
| ADR-01 | Database-per-service trên 1 instance SQL Server | Đúng nguyên tắc microservices, tiết kiệm tài nguyên | Không JOIN xuyên service; cần bản sao dữ liệu |
| ADR-02 | Redis Streams (không dùng Kafka) | Đề cho phép, nhẹ, đã có Redis | Tự viết event bus |
| ADR-03 | Outbox + Inbox | Không lệch giữa DB và sự kiện; không xử lý trùng | Thêm bảng, thêm job |
| ADR-04 | Sự kiện mang dữ liệu (event-carried state) | Render không phải đọc DiagramDb | Payload lớn hơn |
| ADR-05 | Generator PlantUML nằm trong DiagramEngine | Logic UML tập trung 1 chỗ | Render phụ thuộc gRPC |
| ADR-06 | Chỉ BackgroundService + `PeriodicTimer`, **không Hangfire** | Đúng chữ của đề (".NET BackgroundService"); tái sử dụng code v1; không thêm thư viện | Không có dashboard → bù bằng API admin chạy job thủ công + log |
| ADR-07 | **Polling** thay cho realtime (SSE/SignalR/Pub/Sub) | Không phải tiêu chí chấm; ít thành phần, dễ test | Trễ tối đa ~2 s; thêm request định kỳ |
| ADR-08 | Thông báo chỉ trong app, **không email** | Không phải tiêu chí chấm; bớt 1 container và job gửi mail | Người dùng phải mở app mới thấy |
| ADR-09 | Chỉ Redis Streams, không Redis List / Pub/Sub | Một sự kiện tới nhiều service; không mất message | Phải tự xử lý XPENDING/XCLAIM |
