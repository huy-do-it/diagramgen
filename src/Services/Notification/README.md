# Notification — M-02

Theo `docs/TASKS.md` §1, §4/M-02 và BR-33. Solution .NET 8 riêng; API → Service →
Repositories. Chỉ NotificationDb, không đọc IdentityDb/WorkspaceDb/RenderDb hoặc gọi
REST sang service khác. Consumer, Inbox, người nhận từ events và purge thuộc M-05;
UI thuộc M-06; Docker thuộc M-07.

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

Baseline develop `0e86974` chưa có shared Web/Auth/ICurrentUser/PagedResult. Để chạy
M-02 độc lập theo TASKS, API dùng trực tiếp ASP.NET Core JWT Bearer, ProblemDetails,
correlation và Swashbuckle trong composition root của chính Notification. Không tạo
BuildingBlocks/Contracts thay Bảo, không thêm một auth framework hoặc phát token.
`NotificationPage` là response riêng có đúng JSON shape PagedResult; không tạo
generic PagedResult/ICurrentUser trùng với gói chung.

Khi B-01 được publish: thay bootstrap Web/Auth ở API bằng shared defaults và kiểm
chứng lại 401/403/validation/claim mapping/correlation/Swagger. Reuse shared page DTO
khi signature thật sẵn sàng. Không nhận native-bootstrap checks là acceptance B-01.

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

Verification 12/11/2026: Release0warning/error; 19 unit PASS/0FAIL/0SKIP;
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
đã được Minh nghiệm thu và cho phép Git delivery ngày 12/11/2026. Phần tích hợp
B-01/Identity/Gateway vẫn cần kiểm chứng sau, không được suy ra PASS từ kết quả này.
