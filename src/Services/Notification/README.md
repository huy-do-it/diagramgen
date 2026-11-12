# Notification — DB/API, consumers, purge và shared bootstrap

Theo `docs/TASKS.md` §1, §4/M-02 và BR-33. Solution .NET 8 riêng; API → Service →
Repositories. Chỉ NotificationDb, không đọc IdentityDb/WorkspaceDb/RenderDb hoặc gọi
REST sang service khác. M-05 bổ sung ba consumer chung, Inbox, ProjectRecipients và
purge thông báo đã đọc quá 30 ngày; UI thuộc M-06, Docker/Compose thuộc M-07.

## API contract

Mọi endpoint nghiệp vụ yêu cầu JWT hợp lệ. Không nhận `userId` từ client; API lấy
`sub` đã xác thực và truyền Guid vào service. Service kiểm tra ownership trước khi
ghi; repository tiếp tục lọc UserId trong query/update. Admin cũng không được đọc
hoặc sửa notification của user khác.

| Route | Response |
|---|---|
| GET `/api/notifications` | `{items,page,pageSize,totalItems,totalPages}` |
| GET `/api/notifications/unread-count` | `{count}` |
| POST `/api/notifications/{id}/read` | 200 `{isRead:true}`; 403 BR-33 nếu khác owner; 404 nếu không tồn tại |
| POST `/api/notifications/read-all` | 200 `{updated}` |
| POST `/api/admin/notification/dev/seed` | 200 `{created:5}`; chỉ Admin, chỉ map trong Development |

Query: `page>=1`, `pageSize=20` (1–100), `unreadOnly`, `type`, `search`,
`sort=createdAt:desc|createdAt:asc`. Type/search được giới hạn độ dài; sort whitelist;
offset quá lớn trả 400. Lọc/sắp xếp/paging thực hiện trên SQL. Id là Guid; JSON camelCase.
Các DTO notification không có UserId. Response error dùng ProblemDetails với
`traceId`, `code`, `errors` khi validation; không trả exception/connection/token.

`MarkRead`/`MarkAllRead` dùng conditional SQL update `UserId == current && !IsRead`.
Gọi lặp hoặc đồng thời giữ ReadAt ban đầu; read-all không làm mới retention của
notification đã đọc. SQL datetime2 được materialize với UTC Kind để response có `Z`.

## B-01 integration

API dùng `AddUmlForgeDefaults("notification")` và `UseUmlForgeDefaults()` của Bảo:
Serilog, CorrelationContext, JWT, ProblemDetails, Swagger và endpoint chung. Không
sửa BuildingBlocks hoặc triển khai login/refresh của Identity.

Composition root chỉ giữ adaptation cần cho contract Notification: harden JWT HS256
và nonempty Guid sub trên scheme chung; map FluentValidation sang BusinessRuleException
400; giữ traceId/code/errors và correlation header; seed chỉ Development/Admin.
`NotificationDatabaseHealthCheck` kiểm SQL qua endpoint `/health` chung: 200 khi
kết nối được, 503 khi DB không khả dụng; body theo health writer mặc định của shared
Web (text). Startup vẫn chạy migration cho NotificationDb trong mọi environment.

`NotificationPage` giữ response nghiệp vụ đã bàn giao và JSON shape PagedResult.
Không đổi DTO public/schema hoặc tạo thêm auth/event framework trong bước bootstrap.

JWT đúng TASKS §1.2: HS256, issuer `umlforge`, audience `umlforge-client`,
`MapInboundClaims=false`, name `unique_name`, role `role`, clock skew 30s. Signature,
lifetime, issuer/audience và `sub` Guid không rỗng đều được kiểm tra. Key phải được
cung cấp qua secret environment (ít nhất 32 UTF-8 bytes), không có dev-key fallback.
Không triển khai login/refresh/token issuing của Huy.

## Chạy và kiểm thử

Từ thư mục này:

```powershell
dotnet build Notification.sln -c Release /warnaserror
dotnet test Notification.sln -c Release --no-build
# Cung cấp ConnectionStrings__NotificationDb và Jwt__Key bằng secret environment riêng.
# Jwt__Issuer / Jwt__Audience mặc định đã đúng TASKS trong appsettings.json.
dotnet run --project Notification.API
```

Launch profile: `http://localhost:5005/swagger`. Startup migrate DB của service;
`/health` kiểm tra SQL thật. Appsettings không chứa connection hoặc JWT key.
Production cũng có Swagger theo docs; seed route không tồn tại trong Production.
Nút Authorize nhận access token, không phải refresh token.

Máy Minh dùng runtime .NET 8.0.32 riêng do system ASP.NET/Core patch mismatch:

```powershell
$env:DOTNET_ROOT_X64 = 'C:/Users/phamv/AppData/Local/Microsoft/UMLForge/dotnet8'
dotnet build Notification.sln -c Release /warnaserror -p:RuntimeFrameworkVersion=8.0.32
dotnet test Notification.sln -c Release --no-build
```

Verification M-02 (lịch sử, 12/11/2026): Release0warning/error; 19 unit PASS/0FAIL/0SKIP;
44 assertions PASS trên API process + SQL2022 thật, JWT fixture ký riêng ngoài repo.
Bao gồm cách ly hai user/Admin, token sai/hết hạn, paging/filter/search/sort,
ProblemDetails/correlation (kể cả exception), read/read-all lặp/đồng thời, UTC/ReadAt,
migration/recipient PK, Swagger, seed Development/Admin-only và Production restart.

Test đầu phát hiện correlation header bị Response.Clear của exception handler xóa;
đã sửa bằng OnStarting và chạy lại toàn bộ. Compile ban đầu dùng overload Problem
chưa có trong .NET8; đã sửa theo .NET8 và giữ log lỗi riêng. Không giấu FAIL/SKIP.

Evidence ngoài repo ở workspace `assignment-planning/v2-m02-evidence/`. Test runner
đặt ngoài repo, tạo DB fixture riêng trong container SQL cách ly. Không phải
Identity-issued JWT/Gateway/Redis/consumer/browser acceptance. Local implementation
đã được Minh nghiệm thu và cho phép Git delivery ngày 12/11/2026. Không suy ra
acceptance Identity/Gateway từ kết quả của mốc M-02.

## Regression shared bootstrap — 12/11/2026

- Build API Release `/warnaserror`: 0 warning/error; 46 unit PASS, 0 FAIL/SKIP
  (41 test cũ + 5 case cấu hình JWT mới). Tests tham chiếu API để build composition
  code và kiểm options của scheme thật, gồm Events chưa khởi tạo và callback trước.
- 57 SQL/Redis/API checks PASS bằng process API thật: consumer/Inbox/restart/purge
  từ regression M-05, token sai/hết hạn/HS384/Guid-sub, ownership, validation400,
  correlation, sanitized SQL500, Swagger/seed và migration khi restart Production.
- SQL bị tắt có kiểm soát: `/health` trả503, phục hồi200 sau khi SQL chạy lại mà
  không restart API. Image Linux của Notification build PASS.
- Body `/health` dùng text writer chung; lỗi500 dùng code chung `INTERNAL_ERROR`.
  Các route nghiệp vụ, DTO/page shape, BR-33 và code validation không đổi.
- Evidence local ngoài repo: `assignment-planning/v2-notification-bootstrap-evidence/`.
  SQL/Redis fixture riêng đã dọn. Chưa nhận CI, Identity/Gateway hoặc full-stack
  acceptance; bootstrap local cần review/Git delivery riêng.
