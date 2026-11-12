# TASKS — UMLForge v2 · Kế hoạch 4 ngày

> Chia theo **người**, mỗi người sở hữu trọn service của mình (DB, API, consumer, job, Dockerfile, compose, test, màn hình).
> Mọi người code theo **hợp đồng chung ở mục 1** (đã chốt trước), dùng **fake/stub** thay cho service của người khác → làm song song, không chờ nhau.
> Chỉ có **một điểm gặp nhau**: chiều ngày 4 — ghép và chạy demo (mục 7).
>
> Ký hiệu: ⭐ bắt buộc (tiêu chí chấm / demo) · ➕ làm nếu kịp · 🧪 cách tự kiểm tra một mình

---

## 0. Tổng quan

### 0.1 Ai sở hữu gì

| Người | Service | Thư mục (chỉ người này sửa) | Port host | Database | Màn hình |
|---|---|---|---|---|---|
| **Huy** | Identity, Workspace, Gateway | `src/Services/Identity/`, `src/Services/Workspace/`, `src/Gateway/`, `client/src/features/identity/`, `client/src/features/workspace/` | 5001, 5002, 5000 | IdentityDb, WorkspaceDb | UI-01, 02, 03, 04, 05, 10 |
| **Thiện** | Diagram (sơ đồ, phiên bản, render request, review) | `src/Services/Diagram/`, `client/src/features/diagram/` | 5003 | DiagramDb | UI-07, UI-08 |
| **Minh** | Render, Notification | `src/Services/Render/`, `src/Services/Notification/`, `client/src/features/render/`, `client/src/features/notification/` | 5004, 5005 | RenderDb, NotificationDb | UI-09, UI-11 |
| **Bảo** | Contracts, BuildingBlocks, DiagramEngine (gRPC), hạ tầng, CI | `src/Contracts/`, `src/BuildingBlocks/`, `src/Services/DiagramEngine/`, `deploy/`, `.github/`, `tools/`, `client/src/features/editor/`, `client/src/shared/` | 5006 | — | UI-06 |

### 0.2 Lịch 4 ngày

| | Huy | Thiện | Minh | Bảo |
|---|---|---|---|---|
| **Ngày 1 sáng** | H-01 Identity: đăng ký/đăng nhập/JWT | T-01 Diagram: DB + CRUD | M-01 Render: DB + pipeline | **B-01 Khung repo + Contracts + interface chung (xong trước 11:00)** |
| **Ngày 1 chiều** | H-02 Identity: admin user + sự kiện | T-02 Phiên bản + concurrency | M-02 Notification: DB + API | B-02 EventBus + Outbox + ScheduledJobBase |
| **Ngày 2 sáng** | H-03 Workspace: project + thành viên | T-03 Render request + Outbox | M-03 Consumer render + phát sự kiện | B-03 DiagramEngine: chuyển generator/validator v1 |
| **Ngày 2 chiều** | H-04 Workspace: lời mời, giảng viên, danh bạ, job | T-04 Consumer `workspace.events` + `render.events` | M-04 Reconcile, Cleanup, báo cáo ngày | B-04 DiagramEngine: 4 RPC + health |
| **Ngày 3 sáng** | H-05 Gateway YARP | T-05 Gọi gRPC Validate/Suggest | M-05 Notification consumer + job | B-05 Compose hạ tầng + `include` + CI |
| **Ngày 3 chiều** | H-06 Màn hình UI-01/02/03/04/05/10 | T-06 Review ⭐ + bình luận ➕ + ZIP ➕ | M-06 Màn hình UI-09/11 | B-06 Màn hình editor UI-06 + hook polling |
| **Ngày 4 sáng** | H-07 Test BR + Dockerfile + Swagger | T-07 Màn hình UI-07/08 + test BR + Dockerfile | M-07 Test BR + Dockerfile | B-07 Test + công cụ demo + README |
| **Ngày 4 chiều** | **I-01 → I-06: ghép chung, chạy WF-01…03, quay video (cả nhóm)** ||||

> Phụ thuộc duy nhất trước ngày 4: gói **B-01 của Bảo lúc 11:00 ngày 1**. Không ai phải chờ, vì buổi sáng ngày 1 mọi người làm phần CRUD không cần gói chung. Nếu B-01 trễ, cứ code theo **đúng chữ ký ở mục 1.6**.

### 0.3 Năm nguyên tắc để làm độc lập

1. **Chỉ sửa trong thư mục của mình.** File chung (`App.tsx`, `AppLayout.tsx`) mỗi người chỉ thêm đúng 1 dòng.
2. **Mỗi service có file `.sln` riêng** (`src/Services/Identity/Identity.sln`…) → không đụng nhau khi `dotnet sln add`.
3. **Mỗi service có file compose riêng** `deploy/compose/<service>.yml`. File gốc chỉ `include`.
4. **Không chờ người khác — dùng fake:** tự tạo JWT bằng script, tự bắn sự kiện bằng `redis-cli`, dùng `Fake…Client` thay cho gRPC (mục 8).
5. **Không đổi hợp đồng mục 1 trong 4 ngày.** Thấy thiếu field → báo cả nhóm, **chỉ được thêm**, không đổi tên hay xóa.

---

## 1. Hợp đồng chung (đã chốt — mọi người code theo đây)

### 1.1 Port, route, cấu hình

Mọi service lắng nghe **cổng 8080 bên trong container** (mặc định .NET 8). Khi chạy `dotnet run` trên máy, dùng port host ở bảng 0.1 (khai báo trong `Properties/launchSettings.json`).

| Tiền tố route (qua Gateway :5000) | Service | Ghi chú |
|---|---|---|
| `/api/auth/**`, `/api/users/**`, `/api/admin/identity/**` | identity | |
| `/api/projects/**`, `/api/invitations/**`, `/api/directory/**`, `/api/admin/workspace/**` | workspace | |
| `/api/diagrams/**`, `/api/reviews/**`, `/api/admin/diagram/**` | diagram | **Không** dùng `/api/projects/{id}/diagrams` (đụng route Workspace) → dùng `/api/diagrams?projectId=` |
| `/api/render/**`, `/api/admin/render/**` | render | |
| `/api/notifications/**`, `/api/admin/notification/**` | notification | |

Biến môi trường dùng chung (`deploy/.env.example` — giá trị dev, không dùng thật):
```
MSSQL_SA_PASSWORD=Your_password123
JWT_KEY=dev-only-umlforge-signing-key-change-me-32+bytes
JWT_ISSUER=umlforge
JWT_AUDIENCE=umlforge-client
REDIS_URL=redis:6379
DIAGRAM_ENGINE_URL=http://diagram-engine:8080
PLANTUML_URL=http://plantuml:8080
```
Connection string mỗi service: `Server=sqlserver,1433;Database=<Tên>Db;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True`
Khi chạy trên máy: thay `sqlserver` → `localhost`, `redis` → `localhost`.

### 1.2 JWT (mọi service xác thực giống nhau)

| Claim | Giá trị |
|---|---|
| `sub` | UserId (`Guid`) |
| `unique_name` | Username |
| `role` | `Student` · `Lecturer` · `Admin` (Team Leader là quyền theo project, không phải role) |
| `iss` / `aud` | `umlforge` / `umlforge-client` |
| Hạn | Access 15 phút (dev: 8 giờ) · Refresh 7 ngày |

```csharp
// BuildingBlocks.Auth — AddUmlForgeJwt(configuration)
options.MapInboundClaims = false;
options.TokenValidationParameters = new TokenValidationParameters {
    ValidIssuer = cfg["Jwt:Issuer"], ValidAudience = cfg["Jwt:Audience"],
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!)),
    NameClaimType = "unique_name", RoleClaimType = "role", ClockSkew = TimeSpan.FromSeconds(30) };
```

### 1.3 Quy ước API

- Id là `Guid`. Thời gian `DateTime` UTC. JSON camelCase.
- Danh sách: query `page` (≥1), `pageSize` (mặc định 20, ≤100), `search`, `sort=createdAt:desc`, lọc riêng.
  Trả `PagedResult<T> { items, page, pageSize, totalItems, totalPages }`.
- Lỗi: ProblemDetails có thêm `code` (ví dụ `"BR-14"`) và `errors` (lỗi validate).
- Enum dùng chung (JSON dạng chuỗi):
  - `DiagramType`: `1 BusinessContext · 2 UseCase · 3 Sequence · 4 Activity · 5 State` (giữ số như v1)
  - `DiagramStatus`: `Draft · Pending · Rendering · Rendered · Failed`
  - `RenderJobStatus`: `Queued · Running · Done · Failed`
  - `ProjectRole`: `Leader · Member · Lecturer`

### 1.4 Sự kiện — định dạng trên Redis

```
XADD <stream> MAXLEN ~ 10000 * type <TênSựKiện> envelope <JSON>
```
```json
{ "eventId": "guid", "type": "MemberAdded", "version": 1, "occurredAt": "2026-11-12T08:00:00Z",
  "correlationId": "guid", "producer": "workspace", "payload": { } }
```

| Stream | Consumer group (ai đọc) |
|---|---|
| `identity.events` | `workspace-svc` |
| `workspace.events` | `diagram-svc`, `notification-svc` |
| `diagram.events` | `render-svc`, `notification-svc` |
| `render.events` | `diagram-svc`, `notification-svc` |

### 1.5 Danh mục sự kiện (16) — payload

| Sự kiện | Stream | Payload |
|---|---|---|
| `UserRegistered` | identity | `userId, username, fullName, role` |
| `UserRoleChanged` | identity | `userId, role` |
| `UserLocked` | identity | `userId, isLocked` |
| `ProjectCreated` | workspace | `projectId, name, ownerId, actorUserId` |
| `InvitationSent` | workspace | `invitationId, projectId, projectName, inviteeId, actorUserId` |
| `MemberAdded` | workspace | `projectId, projectName, userId, role, actorUserId` |
| `MemberRemoved` | workspace | `projectId, userId, actorUserId` |
| `LecturerAssigned` | workspace | `projectId, projectName, lecturerId, previousLecturerId?, actorUserId` |
| `ProjectDeleted` | workspace | `projectId, actorUserId` |
| `RenderRequested` | diagram | `diagramId, projectId, version, diagramType, title, modelJson, actorUserId` |
| `ReviewSubmitted` | diagram | `reviewId, projectId, actorUserId` |
| `ReviewDecided` | diagram | `reviewId, projectId, decision (Approved/ChangesRequested), note?, actorUserId` |
| `CommentAdded` | diagram | `commentId, diagramId, projectId, diagramTitle, actorUserId` |
| `RenderStarted` | render | `jobId, diagramId, projectId, version` |
| `RenderCompleted` | render | `jobId, diagramId, projectId, version, title, svg, plantUml, durationMs, requestedBy` |
| `RenderFailed` | render | `jobId, diagramId, projectId, version, title, error, requestedBy` |

> `ProjectCreated` ngầm định chủ project là thành viên `Leader` — consumer phải thêm chủ project vào bảng thành viên bản sao.

### 1.6 Interface chung (Bảo cài đặt, mọi người dùng)

```csharp
// UMLForge.Contracts.Events
public sealed record EventEnvelope<T>(Guid EventId, string Type, int Version, DateTime OccurredAt,
                                      Guid CorrelationId, string Producer, T Payload);
public static class StreamNames { public const string Identity = "identity.events", Workspace = "workspace.events",
                                  Diagram = "diagram.events", Render = "render.events"; }

// UMLForge.BuildingBlocks.Outbox
public interface IOutboxDbContext { DbSet<OutboxMessage> OutboxMessages { get; } DbSet<InboxMessage> InboxMessages { get; } }
public interface IOutbox {   // chỉ thêm vào DbContext; được lưu cùng SaveChangesAsync của nghiệp vụ
    void Add<T>(string stream, T payload) where T : class;
}
modelBuilder.ApplyOutboxInbox();                          // gọi trong OnModelCreating
services.AddOutbox<TDbContext>(producer: "workspace");    // đăng ký IOutbox + OutboxPublisher

// UMLForge.BuildingBlocks.EventBus
public interface IEventHandler<T> where T : class { Task HandleAsync(EventEnvelope<T> e, CancellationToken ct); }
services.AddEventConsumer<TDbContext>(StreamNames.Workspace, group: "diagram-svc")
        .On<MemberAdded, MemberAddedHandler>()
        .On<MemberRemoved, MemberRemovedHandler>();       // sự kiện không đăng ký → tự ACK, bỏ qua
public interface IStreamMaintenance {                     // cho ReconcileJob
    Task<IReadOnlyList<PendingEntry>> GetPendingAsync(string stream, string group, TimeSpan minIdle, CancellationToken ct);
    Task ClaimAsync(string stream, string group, string consumer, IEnumerable<string> ids, CancellationToken ct);
    Task MoveToDeadLetterAsync(string stream, string group, string id, CancellationToken ct);
}

// UMLForge.BuildingBlocks.Jobs
public abstract class ScheduledJobBase : BackgroundService {
    public abstract string JobName { get; }               // kebab-case, ví dụ "expire-invitations"
    protected abstract TimeSpan Interval { get; }
    protected virtual bool RunOnStartup => false;
    protected abstract Task<string> RunOnceAsync(IServiceProvider scoped, CancellationToken ct); // trả câu tóm tắt để log
}
services.AddScheduledJob<ExpireInvitationsJob>();
app.MapJobTriggerEndpoint("workspace");   // POST /api/admin/workspace/jobs/{jobName}/run (Admin)

// UMLForge.BuildingBlocks.Web
builder.AddUmlForgeDefaults("workspace");  // Serilog + CorrelationId + ProblemDetails + JWT + Swagger(JWT) + /health
app.UseUmlForgeDefaults();
public interface ICurrentUser { Guid UserId { get; } string Username { get; } string Role { get; } }
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);
public class BusinessRuleException(string code, string message, int status = 409) : Exception(message);  // → ProblemDetails
```

### 1.7 gRPC — `src/Contracts/UMLForge.Contracts.Grpc/Protos/diagram_engine.proto`

**Giữ nguyên tên package, service và 2 RPC của v1** → service gRPC v1 chạy tạm thay được cho tới khi B-04 xong.
```proto
syntax = "proto3";
option csharp_namespace = "UMLForge.Contracts.Grpc";
package diagramengine;

service DiagramEngineService {
  rpc ValidateDiagram  (DiagramValidationRequest)  returns (DiagramValidationResponse);       // v1
  rpc SuggestRelations (RelationSuggestionRequest) returns (RelationSuggestionResponse);      // v1
  rpc GeneratePlantUml (GeneratePlantUmlRequest)   returns (GeneratePlantUmlResponse);        // mới
  rpc ValidateProject  (ProjectValidationRequest)  returns (stream ProjectDiagramValidation);  // mới, streaming
}
// 5 message của v1 giữ nguyên số field:
// DiagramValidationRequest{int32 diagram_type=1; string model_json=2;}
// DiagramValidationResponse{bool is_valid=1; repeated string errors=2; repeated string warnings=3;}
// RelationSuggestionRequest{int32 diagram_type=1; string model_json=2;}
// RelationSuggestionResponse{repeated RelationSuggestion suggestions=1;}
// RelationSuggestion{string from=1; string to=2; string description=3;}
message GeneratePlantUmlRequest  { int32 diagram_type = 1; string model_json = 2; string title = 3; }
message GeneratePlantUmlResponse { string plant_uml = 1; }
message ProjectValidationRequest { repeated ProjectDiagramItem diagrams = 1; }
message ProjectDiagramItem       { string diagram_id = 1; int32 diagram_type = 2; string model_json = 3; }
message ProjectDiagramValidation { string diagram_id = 1; DiagramValidationResponse result = 2; }
```
Phía gọi: `services.AddGrpcClient<DiagramEngineService.DiagramEngineServiceClient>(o => o.Address = new Uri(cfg["DiagramEngine:Url"]!))` + deadline 5 s.
Mỗi bên gọi tự định nghĩa interface mỏng của mình (`IDiagramEngineClient`) để thay bằng fake khi test.

### 1.8 Docker

- Dockerfile mỗi service build với **context = `src/`** (để copy được `Contracts/` và `BuildingBlocks/`):
  `docker build -f Services/Identity/Identity.API/Dockerfile src`
- Mỗi người viết `deploy/compose/<service>.yml` (service + healthcheck `curl -f http://localhost:8080/health`). File gốc `deploy/docker-compose.yml` của Bảo:
```yaml
include:
  - compose/infra.yml        # Bảo: sqlserver, redis, plantuml
  - compose/engine.yml       # Bảo
  - compose/identity.yml     # Huy
  - compose/workspace.yml    # Huy
  - compose/gateway.yml      # Huy
  - compose/diagram.yml      # Thiện
  - compose/render.yml       # Minh
  - compose/notification.yml # Minh
  - compose/client.yml       # Bảo
```

### 1.9 Client

- Làm tiếp trên client v1 (đã có `AuthContext`, `AppLayout`, `api.ts`, `useJobProgress.ts`).
- Mỗi người chỉ làm trong `client/src/features/<của mình>/`, mỗi thư mục có `routes.tsx` export mảng `<Route>`.
  `App.tsx`: mỗi người **thêm 1 dòng** `{identityRoutes}` / `{diagramRoutes}`…
- Khi chưa có Gateway: `client/vite.config.ts` (Bảo làm ở B-01) proxy theo tiền tố ở 1.1 thẳng tới port 5001–5005. Khi có Gateway: đặt `VITE_USE_GATEWAY=true` → mọi `/api` về `http://localhost:5000`.

---

## 2. HUY — Identity · Workspace · Gateway

### H-01 ⭐ Identity: đăng ký, đăng nhập, JWT, refresh (Ngày 1 sáng · BR-01, 02, 03, 05)
**Cách làm**
1. Tạo 3 project + sln riêng:
   ```
   cd src/Services/Identity
   dotnet new sln -n Identity
   dotnet new webapi -n Identity.API --use-controllers
   dotnet new classlib -n Identity.Service
   dotnet new classlib -n Identity.Repositories
   dotnet sln add Identity.API Identity.Service Identity.Repositories
   ```
   Tham chiếu: API → Service → Repositories. (Thêm tham chiếu `BuildingBlocks.*` khi B-01 có.)
2. `Identity.Repositories/Entities`: `User { Id, Username (unique, ≤50), PasswordHash, FullName, Role, IsLocked, CreatedAt }`, `RefreshToken { Id, UserId, TokenHash (unique), ExpiresAt, RevokedAt?, ReplacedById? }`.
3. `IdentityDbContext` + `IUserRepository` (`GetByUsernameAsync`, `UsernameExistsAsync`) + `IRefreshTokenRepository` (`GetByHashAsync`). Chép `GenericRepository` của v1.
4. `Identity.Service`: `IAuthService` với `RegisterAsync`, `LoginAsync`, `RefreshAsync`, `LogoutAsync`; `ITokenService` tạo JWT đúng claim ở 1.2; refresh token = 64 byte ngẫu nhiên, lưu **SHA-256 hash**.
   - BR-01 trùng username → `BusinessRuleException("BR-01", …, 409)` · BR-02 FluentValidation mật khẩu ≥ 8 · BR-03 `IsLocked` → 403 · BR-05 refresh token đã dùng/thu hồi → 401 và **thu hồi cả chuỗi** token đó.
   - BCrypt work factor 11 (package `BCrypt.Net-Next` — v1 đã dùng).
5. `AuthController`: `POST /api/auth/register` (201), `/login` (200 `{accessToken, refreshToken, expiresIn, user}`), `/refresh`, `/logout` (204). `UsersController`: `GET /api/users/me`.
6. `Program.cs`: DbContext + `Database.MigrateAsync()` khi khởi động; seed Development: `admin` (Admin), `lecturer1`, `lecturer2` (Lecturer), `student1…4` (Student) — mật khẩu đọc từ `appsettings.Development.json`, không ghi trong code.
7. Migration: `dotnet ef migrations add InitIdentity -p Identity.Repositories -s Identity.API`.

🧪 Swagger `http://localhost:5001/swagger`: đăng ký → đăng nhập → kiểm claim trong token → gọi `/api/users/me` → refresh 2 lần cùng 1 token, lần 2 phải 401.

### H-02 ⭐ Identity: quản lý user + phát sự kiện (Ngày 1 chiều · BR-03, 04)
**Cách làm**
1. `GET /api/users` (Admin): `search` theo username/fullName, lọc `role`, `isLocked`, sort `username|createdAt`, phân trang → `PagedResult<UserResponse>`.
2. `PATCH /api/users/{id}/lock` `{ isLocked }`, `PATCH /api/users/{id}/role` `{ role }` — chỉ Admin (BR-04); không cho tự khóa chính mình.
3. Khi B-01 có: `IdentityDbContext : IOutboxDbContext`, `modelBuilder.ApplyOutboxInbox()`, `services.AddOutbox<IdentityDbContext>("identity")`, migration `AddOutbox`.
4. Trong service, **trước** `SaveChangesAsync`: `_outbox.Add(StreamNames.Identity, new UserRegistered(...))` (tương tự `UserRoleChanged`, `UserLocked`). Seed user cũng phát `UserRegistered` để Workspace có danh bạ.
5. ➕ `PurgeRefreshTokensJob : ScheduledJobBase` (6 giờ) xóa token hết hạn > 7 ngày; `app.MapJobTriggerEndpoint("identity")`.

🧪 Đăng ký 1 user rồi `docker exec -it umlforge-redis redis-cli XRANGE identity.events - +` → thấy `UserRegistered`.

### H-03 ⭐ Workspace: project + thành viên (Ngày 2 sáng · BR-06, 07, 08)
**Cách làm**
1. Tạo `src/Services/Workspace/` giống H-01 (sln riêng, 3 project, `AddUmlForgeDefaults("workspace")`, `AddOutbox<WorkspaceDbContext>("workspace")`).
2. Entities: `Project { Id, Name, Description, OwnerId, LecturerId?, IsDeleted, CreatedAt, RowVersion }`, `ProjectMember { ProjectId, UserId, Role, JoinedAt }` (PK kép), `Invitation { Id, ProjectId, InviteeId, Status (Pending/Accepted/Declined/Expired), CreatedAt, ExpiresAt }`, `UserDirectory { UserId, Username, FullName, Role, IsLocked }`.
3. API:
   - `GET /api/projects` — project mình là thành viên hoặc giảng viên; `search` theo tên, lọc `role`, sort `name|createdAt`, phân trang.
   - `POST /api/projects` (201) — người tạo thành `Leader` (BR-06) → phát `ProjectCreated`.
   - `GET/PUT/DELETE /api/projects/{id}` — PUT/DELETE chỉ Leader; DELETE xóa mềm → `ProjectDeleted`.
   - `GET /api/projects/{id}/members`; `DELETE /api/projects/{id}/members/{userId}` — chỉ Leader (BR-08), không xóa Leader → `MemberRemoved`.
4. Kiểm tra quyền trong Service bằng `ICurrentUser.UserId` + bảng `ProjectMember`.

🧪 Tạo token bằng `tools/make-dev-token.ps1` (mục 8.1), gọi Swagger :5002; người không phải thành viên nhận 403.

### H-04 ⭐ Workspace: lời mời, giảng viên, danh bạ, job (Ngày 2 chiều · BR-07, 08, 09, 10)
**Cách làm**
1. Consumer danh bạ: `services.AddEventConsumer<WorkspaceDbContext>(StreamNames.Identity, "workspace-svc").On<UserRegistered, …>().On<UserRoleChanged, …>().On<UserLocked, …>()` → upsert `UserDirectory`.
2. `GET /api/directory/users?search=&role=Lecturer` (phân trang) — dùng cho ô mời thành viên và chọn giảng viên.
3. `POST /api/projects/{id}/invitations` `{ username }` — chỉ Leader; user phải có trong `UserDirectory`, không bị khóa; nhóm đã đủ 5 (tính cả lời mời Pending) → 409 BR-07 → phát `InvitationSent`.
4. `GET /api/invitations/mine`; `POST /api/invitations/{id}/accept` (hết hạn → 410 BR-09; chấp nhận → thêm `Member` + phát `MemberAdded`), `POST /api/invitations/{id}/decline`.
5. `PUT /api/projects/{id}/lecturer` `{ lecturerId }` — chỉ Leader; user phải có role Lecturer trong `UserDirectory` (BR-10) → `LecturerAssigned`.
6. `ExpireInvitationsJob : ScheduledJobBase` (1 giờ): Pending quá `ExpiresAt` → Expired. `app.MapJobTriggerEndpoint("workspace")`.

🧪 `tools/seed-events.ps1 identity` bắn `UserRegistered` cho 6 user (mục 8.2); mời người thứ 6 → 409; sửa `ExpiresAt` trong DB về quá khứ, gọi `POST /api/admin/workspace/jobs/expire-invitations/run` → lời mời thành Expired.

### H-05 ⭐ Gateway YARP (Ngày 3 sáng)
**Cách làm**
1. `dotnet new web -n UMLForge.Gateway` trong `src/Gateway/`, package `Yarp.ReverseProxy`.
2. `appsettings.json` → `ReverseProxy.Routes` đúng **bảng 1.1** (1 route / tiền tố), `Clusters` trỏ `http://identity:8080`… (`appsettings.Development.json`: `http://localhost:5001`…).
3. JWT: Gateway kiểm tra token cho route cần đăng nhập (`AuthorizationPolicy = "default"`), route `/api/auth/**` để `anonymous`.
4. CORS cho `http://localhost:3000` và `http://localhost:5173`; rate limit `/api/auth/login` 10 lần/phút/IP (`AddRateLimiter` có sẵn .NET 8).
5. Chuyển tiếp header `X-Correlation-Id` (tạo mới nếu thiếu).
6. `/swagger` của Gateway: trang HTML đơn giản liệt kê link Swagger 5 service.

🧪 Chạy Gateway + Identity: `POST http://localhost:5000/api/auth/login` thành công; route của service chưa chạy trả 502 (bình thường).

### H-06 ⭐ Màn hình UI-01, 02, 03, 04, 05, 10 (Ngày 3 chiều)
**Cách làm**
1. `client/src/features/identity/`: chuyển `LoginPage`, `RegisterPage` v1 theo response mới (Guid, `refreshToken`); `AuthContext` tự gọi `/api/auth/refresh` khi gặp 401 một lần; `AdminUsersPage` (UI-10): bảng + tìm kiếm + lọc role + phân trang + khóa/đổi role.
2. `client/src/features/workspace/`: `ProjectsPage` (UI-03: tìm/lọc/sắp xếp/phân trang), `ProjectDetailPage` (UI-04: tab Thành viên + nút gán giảng viên; tab "Sơ đồ" và "Review" chừa chỗ gắn component `DiagramListPanel`, `ReviewPanel` của Thiện), `InviteMemberModal` (UI-05, ô tìm từ `/api/directory/users`), trang "Lời mời của tôi".
3. `routes.tsx` + thêm 1 dòng vào `App.tsx`.

🧪 Chạy client với proxy thẳng (1.9) + Identity + Workspace.

### H-07 ⭐ Hoàn thiện (Ngày 4 sáng)
- Unit test BR-01 → BR-10 (mỗi BR ≥ 1 test, `[Trait("BR","BR-07")]`), test handler `UserRegistered` nhận trùng.
- Dockerfile (multi-stage, context `src/`) cho Identity, Workspace, Gateway + `deploy/compose/{identity,workspace,gateway}.yml`.
- Kiểm tra Swagger có `[ProducesResponseType]` đủ mã lỗi.

---

## 3. THIỆN — Diagram service

### T-01 ⭐ Diagram: DB + CRUD (Ngày 1 sáng · BR-11, 12, 13, 32)
**Cách làm**
1. Tạo `src/Services/Diagram/` (sln riêng, 3 project). Chép `DiagramType` và các model sơ đồ (`UseCaseModel`, `SequenceModel`…) từ `PRN232.DiagramGen.Services` v1 vào `Diagram.Service/Models` để parse JSON kiểm tra cơ bản.
2. Entities: `Diagram { Id, ProjectId, Type, Title, ModelJson, Status, CurrentVersion, SvgXml?, PlantUml?, RenderedAt?, LastError?, IsDeleted, CreatedBy, CreatedAt, UpdatedAt, RowVersion }`, `MemberAccess { ProjectId, UserId, Role }` (PK kép).
3. API:
   - `GET /api/diagrams?projectId=&type=&status=&search=&sort=updatedAt:desc&page=&pageSize=` → `PagedResult<DiagramSummary>` (không trả `ModelJson`, `SvgXml`).
   - `POST /api/diagrams` `{ projectId, type, title, modelJson }` (201) · `GET /api/diagrams/{id}` · `PUT /api/diagrams/{id}` · `DELETE /api/diagrams/{id}` (xóa mềm, 204).
4. Quyền (BR-11): mọi thao tác kiểm `MemberAccess (projectId, userId)`. Lecturer chỉ được GET (BR-32).
5. BR-12: `ModelJson` parse được theo `Type`, ≤ 256 KB (FluentValidation). BR-13: `PUT` khi Status ∈ {Pending, Rendering} → 409.
6. **Chưa có sự kiện Workspace?** Thêm endpoint `POST /api/admin/diagram/dev/member-access` (chỉ map khi `IsDevelopment()`) để tự thêm quyền.

🧪 Swagger :5003 với token từ `make-dev-token.ps1`; user không có `MemberAccess` → 403.

### T-02 ⭐ Phiên bản + concurrency (Ngày 1 chiều · BR-15, 16, 17)
**Cách làm**
1. Entity `DiagramVersion { Id, DiagramId, Version, Title, ModelJson, CreatedBy, CreatedAt }` unique `(DiagramId, Version)`.
2. Tạo diagram → version 1; mỗi `PUT` thành công → `CurrentVersion++` + thêm 1 dòng `DiagramVersion` trong **cùng** `SaveChangesAsync` (BR-15).
3. `GET /api/diagrams/{id}` trả header `ETag: "<RowVersion base64>"`; `PUT` bắt buộc `If-Match`, sai → 409 BR-16 (bắt `DbUpdateConcurrencyException`).
4. `PUT` khi Status ∈ {Rendered, Failed} → Status = `Draft`, **giữ** `SvgXml` cũ để xem (BR-17).
5. `GET /api/diagrams/{id}/versions` (phân trang) · `GET /api/diagrams/{id}/versions/{version}` · ➕ `POST /api/diagrams/{id}/versions/{version}/restore`.

🧪 Mở 2 tab Swagger cùng ETag, PUT tab 1 rồi tab 2 → tab 2 nhận 409.

### T-03 ⭐ Yêu cầu render qua Outbox (Ngày 2 sáng · BR-14)
**Cách làm**
1. `DiagramDbContext : IOutboxDbContext`, `ApplyOutboxInbox()`, `AddOutbox<DiagramDbContext>("diagram")`, migration `AddOutbox`.
2. `POST /api/diagrams/{id}/render`: Status ∈ {Pending, Rendering} → 409 BR-14; ngược lại Status = `Pending` + `_outbox.Add(StreamNames.Diagram, new RenderRequested(...))` (đủ field ở 1.5) → **cùng 1 SaveChanges** → trả **202** `{ diagramId, version, status }`.
3. Client sẽ polling `GET /api/diagrams/{id}` — giữ endpoint này nhẹ (`AsNoTracking`, projection).

🧪 Gọi render → `redis-cli XRANGE diagram.events - +` thấy `RenderRequested`; gọi lần 2 → 409.

### T-04 ⭐ Consumer `workspace.events` + `render.events` (Ngày 2 chiều · BR-28)
**Cách làm**
1. `AddEventConsumer<DiagramDbContext>(StreamNames.Workspace, "diagram-svc")`:
   `ProjectCreated` → thêm owner `Leader` · `MemberAdded` → upsert · `MemberRemoved` → xóa · `LecturerAssigned` → xóa lecturer cũ, thêm `Lecturer` · `ProjectDeleted` → xóa quyền + xóa mềm diagram của project.
2. `AddEventConsumer<DiagramDbContext>(StreamNames.Render, "diagram-svc")`:
   `RenderStarted` → `Rendering` · `RenderCompleted` → `SvgXml`, `PlantUml`, `RenderedAt`, `Rendered` · `RenderFailed` → `Failed`, `LastError`, **giữ SVG cũ** (BR-28).
   **Bỏ qua** sự kiện có `version` < `CurrentVersion` (diagram đã được sửa sau khi gửi render).

🧪 `tools/seed-events.ps1 workspace` bắn `ProjectCreated` + `MemberAdded` → kiểm `MemberAccess`; `tools/seed-events.ps1 render` bắn `RenderCompleted` → diagram thành `Rendered`.

### T-05 ⭐ Gọi gRPC Validate / Suggest / ValidateProject (Ngày 3 sáng · BR-18…25)
**Cách làm**
1. Tham chiếu `UMLForge.Contracts.Grpc`. Tạo `IDiagramEngineClient` trong `Diagram.Service` + `GrpcDiagramEngineClient` + `FakeDiagramEngineClient` (luôn hợp lệ). Chọn bằng cấu hình `DiagramEngine:UseFake`.
2. `POST/PUT` diagram: gọi `ValidateDiagram`; có `errors` → 422 kèm danh sách lỗi; `warnings` trả kèm response 200/201.
3. `POST /api/diagrams/{id}/validate` → trả kết quả (không lưu). `GET /api/diagrams/{id}/suggestions` → `SuggestRelations`.
4. `GET /api/diagrams/validate-project?projectId=` → gọi `ValidateProject` (streaming), gom thành danh sách trả về.
5. gRPC lỗi `Unavailable`/hết deadline → 503 ProblemDetails "Dịch vụ kiểm tra UML tạm thời không phản hồi".

🧪 Trước khi B-04 xong: chạy **service gRPC v1** (`dotnet run --project PRN232.DiagramGen.GrpcService`) và trỏ `DiagramEngine:Url` vào nó — Validate/Suggest chạy thật; `ValidateProject` dùng fake.

### T-06 Review ⭐ · Bình luận ➕ · Xuất ZIP ➕ (Ngày 3 chiều · BR-29, 30, 31, 32)
**Cách làm**
1. Entities `Review { Id, ProjectId, Status (Submitted/Approved/ChangesRequested), SubmittedBy, SubmittedAt, DecidedBy?, DecidedAt?, Note?, SnapshotJson }`, ➕ `Comment { Id, DiagramId, AuthorId, Content (≤2000), CreatedAt }`.
2. ⭐ `POST /api/reviews` `{ projectId }` — chỉ Leader (`MemberAccess.Role == Leader`); mọi diagram chưa xóa phải `Rendered` (BR-29, 400 kèm danh sách chưa đạt); snapshot = danh sách `{diagramId, version}` → `ReviewSubmitted`.
3. ⭐ `GET /api/reviews?projectId=&status=` (phân trang) · `POST /api/reviews/{id}/approve` · `POST /api/reviews/{id}/request-changes` `{ note }` — chỉ Lecturer của project (BR-30) → `ReviewDecided`.
4. ⭐ BR-31: `PUT` diagram thuộc project có review mới nhất `Approved` → response có thêm `needsResubmit: true` (không chặn).
5. ➕ `GET/POST /api/diagrams/{id}/comments` → `CommentAdded`. Lecturer chỉ đọc + bình luận (BR-32).
6. ➕ `GET /api/diagrams/export?projectId=` → ZIP (`System.IO.Compression`) gồm `<title>.svg`, `.puml`, `.json`.

🧪 Token Leader / Lecturer / Student khác nhau từ `make-dev-token.ps1` + `MemberAccess` tương ứng.

### T-07 ⭐ Màn hình + hoàn thiện (Ngày 4 sáng)
1. `client/src/features/diagram/`: `DiagramListPanel` (gắn vào UI-04: lọc loại/trạng thái, tìm, phân trang, nút Render), `DiagramViewPage` (UI-07: SVG, lịch sử phiên bản, trạng thái tự cập nhật bằng `useRenderStatus` của Bảo — nếu chưa có thì tạm `setInterval` 2 s), `ReviewPanel` (UI-08).
2. Unit test BR-11 → BR-17, BR-29 → BR-32; test handler `RenderCompleted` nhận trùng + version cũ.
3. Dockerfile + `deploy/compose/diagram.yml`.

---

## 4. MINH — Render · Notification

### M-01 ⭐ Render: DB + pipeline (Ngày 1 sáng · BR-27)
**Cách làm**
1. Tạo `src/Services/Render/` gồm `Render.Worker` (`dotnet new web` — vừa chạy BackgroundService vừa có API admin), `Render.Service`, `Render.Repositories` + sln riêng.
2. Entities: `RenderJob { Id, DiagramId, ProjectId, DiagramVersion, DiagramType, Title, ModelJson, RequestedBy, Status, Attempt, Error?, CreatedAt, StartedAt?, FinishedAt?, DurationMs? }` unique `(DiagramId, DiagramVersion)`, `RenderArtifact { JobId, PlantUml, SvgXml }`, `RenderDailyReport { Date (PK), Total, Done, Failed, AvgMs, SuccessRate }`.
3. Chép **`IRenderPipeline` + `RenderPipeline` của v1** (`PRN232.DiagramGen.Worker`) vào `Render.Service`, đổi đầu vào thành `RenderJob`; bước sinh PlantUML gọi `IDiagramEngineClient.GeneratePlantUmlAsync`; bước gọi PlantUML server giữ nguyên (`HttpClient` timeout **30 s**, BR-27).
4. `IDiagramEngineClient` + `FakeDiagramEngineClient` (trả `@startuml\nAlice -> Bob: hello\n@enduml`) + `GrpcDiagramEngineClient`. Cấu hình `DiagramEngine:UseFake`.

🧪 Unit test pipeline với fake + PlantUML server thật (`docker run -d -p 8085:8080 plantuml/plantuml-server:jetty`).

### M-02 ⭐ Notification: DB + API (Ngày 1 chiều · BR-33)
**Cách làm**
1. Tạo `src/Services/Notification/` (sln riêng, 3 project).
2. Entities: `Notification { Id, UserId, Type, Title, Link, IsRead, ReadAt?, CreatedAt }` index `(UserId, IsRead, CreatedAt)`, `ProjectRecipient { ProjectId, UserId, Role, ProjectName }`.
3. API (chỉ dữ liệu của chính mình — BR-33):
   `GET /api/notifications?unreadOnly=&type=&page=&pageSize=` · `GET /api/notifications/unread-count` → `{ count }` · `POST /api/notifications/{id}/read` (của người khác → 403) · `POST /api/notifications/read-all`.
4. `POST /api/admin/notification/dev/seed` (chỉ Development) tạo 5 thông báo mẫu cho user hiện tại.

🧪 Swagger :5005 với 2 token khác nhau.

### M-03 ⭐ Consumer `RenderRequested` + phát sự kiện (Ngày 2 sáng · BR-26)
**Cách làm**
1. `RenderDbContext : IOutboxDbContext`, `AddOutbox<RenderDbContext>("render")`.
2. `AddEventConsumer<RenderDbContext>(StreamNames.Diagram, "render-svc").On<RenderRequested, RenderRequestedHandler>()`.
3. Handler:
   - Đã có job `(DiagramId, Version)` → bỏ qua (BR-26).
   - Tạo job `Running` + Outbox `RenderStarted` → **SaveChanges** (để Diagram thấy "Đang render").
   - Chạy pipeline → thành công: `RenderArtifact` + `Done` + Outbox `RenderCompleted`; lỗi nghiệp vụ/timeout: `Failed` + `Error` + Outbox `RenderFailed` → SaveChanges.
   - Lỗi hạ tầng (SQL/Redis chết) → **ném exception** để không ACK, Reconcile xử lý lại.
4. Chạy 2 bản Render cùng lúc vẫn đúng (consumer group chia message).

🧪 `tools/seed-events.ps1 diagram` bắn 1 `RenderRequested` mẫu → `XRANGE render.events - +` thấy `RenderStarted` rồi `RenderCompleted`; bắn lại cùng version → không tạo job mới.

### M-04 ⭐ Reconcile, Cleanup, báo cáo ngày + API admin (Ngày 2 chiều · NV-08)
**Cách làm** (đều kế thừa `ScheduledJobBase`, chép logic từ v1 `ReconcileHostedService`, `CleanupHostedService`)
1. `ReconcileJob` (2 phút): dùng `IStreamMaintenance` — message trong `XPENDING diagram.events render-svc` idle > 60 s: `deliveryCount ≥ 5` → chuyển `diagram.events.dlq` + ACK; còn lại `XCLAIM` về consumer hiện tại. Job `Running` > 5 phút → `Failed` + `RenderFailed`.
2. `CleanupJob` (1 giờ): xóa job `Failed` > 7 ngày, artifact không còn job, Inbox/Outbox đã xử lý > 3 ngày.
3. `DailyRenderReportJob` (1 giờ, `RunOnStartup = true`): nếu chưa có báo cáo hôm qua → tính Total/Done/Failed/AvgMs/SuccessRate → lưu.
4. API (Admin): `GET /api/render/jobs?status=&diagramId=&from=&to=&page=` · `POST /api/render/jobs/{id}/retry` (chỉ job `Failed`: chạy lại pipeline từ `ModelJson` đã lưu trong job, `Attempt++`) · `GET /api/render/reports?from=&to=&page=`.
5. `app.MapJobTriggerEndpoint("render")`.

🧪 Tắt Render khi còn message chưa xử lý → bật lại → `POST /api/admin/render/jobs/reconcile/run` → message được xử lý tiếp.

### M-05 ⭐ Notification: consumer + job (Ngày 3 sáng · BR-34, 35)
**Cách làm**
1. `NotificationDbContext : IOutboxDbContext` (chỉ dùng Inbox).
2. `AddEventConsumer<…>(StreamNames.Workspace, "notification-svc")`: `ProjectCreated`/`MemberAdded`/`MemberRemoved`/`LecturerAssigned`/`ProjectDeleted` → cập nhật `ProjectRecipient`; `InvitationSent` → báo `inviteeId`; `MemberAdded` → báo Leader.
3. `AddEventConsumer<…>(StreamNames.Diagram, "notification-svc")`: `ReviewSubmitted` → báo Lecturer; `ReviewDecided` → báo mọi thành viên; `CommentAdded` → báo thành viên.
4. `AddEventConsumer<…>(StreamNames.Render, "notification-svc")`: `RenderCompleted`/`RenderFailed` → báo **`requestedBy`**.
5. BR-34: **không** tạo thông báo cho `actorUserId`. Mỗi thông báo có `Link` (ví dụ `/diagrams/{id}`, `/projects/{id}?tab=review`).
6. `PurgeReadNotificationsJob` (6 giờ): xóa thông báo đã đọc > 30 ngày (BR-35). `MapJobTriggerEndpoint("notification")`.

🧪 `tools/seed-events.ps1 workspace` + `render` → mỗi user thấy đúng thông báo của mình; người thực hiện không nhận.

### M-06 ⭐ Màn hình UI-09, UI-11 (Ngày 3 chiều)
1. `client/src/features/notification/`: `NotificationBell` (gắn vào Topbar — thêm 1 dòng trong `AppLayout`; polling `unread-count` 30 s, dropdown 10 mục), `NotificationsPage` (UI-09: lọc chưa đọc, phân trang, đánh dấu tất cả đã đọc).
2. `client/src/features/render/`: `AdminRenderPage` (UI-11): bảng job (lọc trạng thái, phân trang, nút Retry), bảng báo cáo ngày, khu "Job định kỳ" có nút **Chạy ngay** cho từng job của 5 service (gọi `/api/admin/<svc>/jobs/<name>/run`).

### M-07 ⭐ Hoàn thiện (Ngày 4 sáng)
- Unit test BR-26, 27, 28, 33, 34, 35; handler nhận trùng; pipeline timeout.
- Dockerfile Render (tham khảo Dockerfile Worker v1) + Notification; `deploy/compose/{render,notification}.yml`.

---

## 5. BẢO — Contracts · BuildingBlocks · DiagramEngine · Hạ tầng · Editor

### B-01 ⭐ Khung repo + hợp đồng (Ngày 1, **xong trước 11:00**)
**Cách làm**
1. Tạo cấu trúc thư mục theo ARCHITECTURE §4, `src/Directory.Build.props` (`net8.0`, `Nullable enable`, `TreatWarningsAsErrors` khi CI), `.editorconfig`, `.gitignore`, `deploy/.env.example` (mục 1.1).
2. `UMLForge.Contracts.Events`: `EventEnvelope<T>`, `StreamNames`, 16 record ở 1.5, `EventTypeRegistry` (tên → `Type`).
3. `UMLForge.Contracts.Grpc`: proto ở 1.7 (`GrpcServices="Both"`).
4. `UMLForge.BuildingBlocks.{Web,Auth,Outbox,EventBus,Jobs}`: **khai báo đủ chữ ký ở 1.6** + phần Web/Auth chạy được ngay (ProblemDetails, CorrelationId, Serilog, JWT, Swagger có nút Authorize, `/health`, `ICurrentUser`). Outbox/EventBus/Jobs có thể còn `NotImplementedException` ở bước này.
5. `tools/make-dev-token.ps1` (8.1), `client/vite.config.ts` proxy theo 1.1, thư mục `client/src/features/*` + `routes.tsx` mẫu, mỗi file `deploy/compose/*.yml` rỗng có comment hướng dẫn.
6. Báo nhóm: "B-01 xong, kéo `develop`".

🧪 Một API mẫu tham chiếu `BuildingBlocks.Web` chạy được Swagger + `/health`.

### B-02 ⭐ EventBus + Outbox + ScheduledJobBase (Ngày 1 chiều)
**Cách làm**
1. Outbox: `OutboxMessage { Id, Stream, Type, Envelope, CreatedAt, PublishedAt?, Attempts, LastError? }`, `InboxMessage { EventId, Consumer, ProcessedAt }` PK `(EventId, Consumer)`. `IOutbox.Add` tạo envelope (lấy `CorrelationId` từ header/`Activity`). `OutboxPublisher<TDb>` (BackgroundService, 1 s): lấy 50 dòng chưa gửi → `XADD … MAXLEN ~ 10000` → đánh dấu `PublishedAt`.
2. EventBus: `EventConsumer<TDb>` (BackgroundService): tạo group nếu chưa có (`XGROUP CREATE … $ MKSTREAM`, bỏ qua lỗi BUSYGROUP) → vòng `XREADGROUP COUNT 10 >`, nghỉ 200 ms nếu rỗng → mỗi message: tạo scope, kiểm `InboxMessage` → gọi `IEventHandler<T>` → thêm Inbox → SaveChanges → `XACK`. Lỗi → log, **không ACK**.
3. `IStreamMaintenance`: `GetPendingAsync`, `ClaimAsync`, `MoveToDeadLetterAsync` (cho ReconcileJob của Minh).
4. `ScheduledJobBase` dùng `PeriodicTimer`; mỗi lần chạy tạo scope; bắt mọi exception; log `[JobName] <tóm tắt> in <ms> ms`. `IJobTrigger` + `MapJobTriggerEndpoint(service)`.
5. Integration test (Testcontainers Redis + SQL): gửi 1 → nhận 1; gửi trùng `eventId` → handler chạy 1 lần; handler ném lỗi → message còn trong `XPENDING`.

### B-03 ⭐ DiagramEngine: chuyển generator + validator v1 (Ngày 2 sáng · BR-18…25)
**Cách làm**
1. `src/Services/DiagramEngine/DiagramEngine.Grpc` (`dotnet new grpc`) + `DiagramEngine.Tests`, sln riêng. Kestrel: `EndpointDefaults.Protocols = Http2` (h2c trong mạng Docker).
2. Chép từ v1: `PlantUmlGenerator` + model 5 loại sơ đồ (`PRN232.DiagramGen.Services`) → `Generators/`; `DiagramValidators` + luật trong `DiagramEngineServiceImpl` (GrpcService v1) → `Validators/`. Đổi namespace, **không sửa logic**.
3. Chép test generator/validator từ `tests/PRN232.DiagramGen.UnitTests` → chạy xanh.

### B-04 ⭐ DiagramEngine: 4 RPC + health (Ngày 2 chiều)
**Cách làm**
1. `DiagramEngineGrpcService : DiagramEngineService.DiagramEngineServiceBase`: `ValidateDiagram`, `SuggestRelations` (như v1), `GeneratePlantUml` (validate trước, lỗi → `RpcException(InvalidArgument)`), `ValidateProject` (duyệt từng diagram, `await responseStream.WriteAsync(...)` từng kết quả).
2. Interceptor: log tên RPC + thời gian + `x-correlation-id`. `AddGrpcHealthChecks()` + `MapGrpcHealthChecksService()`.
3. Bật gRPC reflection ở Development để test bằng Postman.

🧪 Postman (gRPC request) gọi cả 4 RPC với JSON mẫu CampusEats của v1.

### B-05 ⭐ Compose hạ tầng + CI (Ngày 3 sáng)
**Cách làm**
1. `deploy/compose/infra.yml`: `sqlserver` (2022, healthcheck `sqlcmd -Q "SELECT 1"`), `redis` (7, `--appendonly yes`, healthcheck `redis-cli ping`, `container_name: umlforge-redis`), `plantuml`. Network chung `umlforge`.
2. `deploy/compose/engine.yml`, `deploy/compose/client.yml` (client build nginx, `/api` → `gateway:8080`).
3. `deploy/docker-compose.yml` với `include` (1.8).
4. `.github/workflows/ci.yml`: matrix theo từng `.sln` (`dotnet build -warnaserror` + `dotnet test`) + job build client.

### B-06 ⭐ Màn hình editor UI-06 + hook polling (Ngày 3 chiều)
**Cách làm**
1. `client/src/features/editor/`: chuyển `DiagramEditorPage`, `DiagramExamplePanel`, `templates/diagramTemplates.ts`, `utils/diagramJson.ts` của v1 sang; đổi API sang `/api/diagrams` (Guid, `If-Match`, hiển thị lỗi 422 từ gRPC, cảnh báo, gợi ý quan hệ).
2. `client/src/shared/useRenderStatus.ts`: dựa trên `useJobProgress.ts` v1 — polling `GET /api/diagrams/{id}` 2 s khi `Pending/Rendering`, dừng khi `Rendered/Failed`, trả `{ status, svg, error }`.
3. Báo Thiện dùng hook này cho UI-07.

### B-07 ⭐ Hoàn thiện + công cụ demo (Ngày 4 sáng)
- `tools/seed-events.ps1` + `tools/events/<stream>/*.json` (8.2), `tools/reset-demo.ps1` (xóa volume → `compose up` → chờ healthy → tạo dữ liệu CampusEats qua Gateway).
- Test DiagramEngine (BR-18…25 + streaming). README: kiến trúc, cách chạy, tài khoản demo (trỏ tới `appsettings.Development.json`), link Swagger, cách chạy job thủ công.

---

## 6. Bảng kiểm tra không phụ thuộc chéo

| Người | Cần gì của người khác? | Thay thế khi chưa có |
|---|---|---|
| Huy | Gói chung B-01 | Chữ ký ở 1.6 (sáng ngày 1 chưa cần) |
| Thiện | Sự kiện Workspace (Huy), gRPC (Bảo), sự kiện Render (Minh), hook polling (Bảo) | Endpoint dev `member-access`; gRPC v1 / `FakeDiagramEngineClient`; `seed-events.ps1`; `setInterval` tạm |
| Minh | `RenderRequested` (Thiện), gRPC `GeneratePlantUml` (Bảo), sự kiện Workspace/Diagram | `seed-events.ps1`; `FakeDiagramEngineClient` |
| Bảo | Không | — |
| Tất cả | JWT (Huy), Gateway (Huy) | `make-dev-token.ps1`; Vite proxy thẳng tới service |

---

## 7. Ngày 4 chiều — ghép chung (cả nhóm, ~4 giờ)

| ID | Việc | Người dẫn | Xong khi |
|---|---|---|---|
| I-01 | Merge tất cả vào `develop`; đặt mọi `UseFake = false` | Bảo | CI xanh |
| I-02 | `docker compose -f deploy/docker-compose.yml up --build -d` | Bảo | 11 container healthy |
| I-03 | Chạy WF-01 (tạo project, mời, chấp nhận, thông báo) | Huy | Đúng PRD §8, kể cả ngoại lệ 409 |
| I-04 | Chạy WF-02 (soạn Use Case, lỗi gRPC, render, tắt Render giữa chừng → tự phục hồi) | Thiện + Minh | Đúng PRD §8 |
| I-05 | Chạy WF-03 (gửi duyệt, Lecturer duyệt, xuất ZIP nếu có) | Thiện | Đúng PRD §8 |
| I-06 | Chạy thủ công các job định kỳ, chụp log; quay video demo dự phòng 3–4 phút | Minh + Bảo | Có video + ảnh minh chứng |

Lỗi phát hiện → ghi vào PROGRESS.md mục 4, người sở hữu service sửa.

---

## 8. Công cụ tự test (Bảo tạo ở B-01/B-07, ai cũng dùng được)

### 8.1 Tạo JWT dev — `tools/make-dev-token.ps1`
```powershell
param([string]$UserId = [guid]::NewGuid(), [string]$Role = "Student", [string]$Name = "student1")
$key = if ($env:JWT_KEY) { $env:JWT_KEY } else { "dev-only-umlforge-signing-key-change-me-32+bytes" }
function B64Url([byte[]]$b) { [Convert]::ToBase64String($b).TrimEnd('=').Replace('+','-').Replace('/','_') }
$enc = [Text.Encoding]::UTF8
$h = B64Url $enc.GetBytes('{"alg":"HS256","typ":"JWT"}')
$exp = [DateTimeOffset]::UtcNow.AddHours(8).ToUnixTimeSeconds()
$p = B64Url $enc.GetBytes((@{ sub="$UserId"; unique_name=$Name; role=$Role; iss="umlforge"; aud="umlforge-client"; exp=$exp } | ConvertTo-Json -Compress))
$s = B64Url ([Security.Cryptography.HMACSHA256]::new($enc.GetBytes($key))).ComputeHash($enc.GetBytes("$h.$p"))
"UserId: $UserId"; "$h.$p.$s"
```
Dùng: `./tools/make-dev-token.ps1 -Role Lecturer -Name lecturer1` → dán token vào nút **Authorize** của Swagger. Ghi lại `UserId` để thêm `MemberAccess` / bắn sự kiện cho đúng người.

### 8.2 Bắn sự kiện giả — `tools/seed-events.ps1 <identity|workspace|diagram|render>`
Đọc các file mẫu `tools/events/<stream>/*.json` (envelope đầy đủ, Id cố định dễ nhớ) và chạy cho từng file:
```powershell
docker exec umlforge-redis redis-cli XADD "$stream.events" '*' type $type envelope $json
```
Bắn tay trong `docker exec -it umlforge-redis redis-cli`:
```
XADD render.events * type RenderCompleted envelope "{\"eventId\":\"6f1c0000-0000-0000-0000-000000000001\",\"type\":\"RenderCompleted\",\"version\":1,\"occurredAt\":\"2026-11-12T08:00:00Z\",\"correlationId\":\"6f1c0000-0000-0000-0000-000000000002\",\"producer\":\"render\",\"payload\":{\"jobId\":\"...\",\"diagramId\":\"...\",\"projectId\":\"...\",\"version\":1,\"title\":\"Use Case\",\"svg\":\"<svg/>\",\"plantUml\":\"@startuml\\n@enduml\",\"durationMs\":120,\"requestedBy\":\"...\"}}"
```
Xem: `XRANGE <stream> - +` · `XINFO GROUPS <stream>` · `XPENDING <stream> <group>`.

### 8.3 Chạy riêng 1 service trên máy
```bash
docker compose -f deploy/compose/infra.yml up -d
```
```bash
dotnet run --project src/Services/Diagram/Diagram.API
```

---

## 9. Nếu không kịp — cắt theo thứ tự
1. T-06 xuất ZIP → 2. T-06 bình luận → 3. T-02 khôi phục phiên bản → 4. H-02 `PurgeRefreshTokensJob` → 5. Gợi ý quan hệ (`SuggestRelations`) ở client.

**Không cắt:** Outbox/Inbox, Redis Streams, gRPC (≥ 1 service gọi), BackgroundService (consumer + ≥ 2 job định kỳ), tìm/lọc/sắp xếp/phân trang, JWT, Docker Compose — đây là tiêu chí chấm.
