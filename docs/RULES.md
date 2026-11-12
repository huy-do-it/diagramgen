# RULES — UMLForge v2

> Quy tắc viết code cho cả nhóm và những điều **AI tuyệt đối không được làm**.
> Đọc file này trước khi viết dòng code đầu tiên. Khi nhờ AI (Claude, Copilot…), dán mục 9 vào đầu prompt.

---

## 1. Kiến trúc — luật cứng

| # | Luật | Vi phạm thì sao |
|---|---|---|
| A1 | Mỗi service **chỉ** đọc/ghi database của chính nó | PR bị từ chối |
| A2 | Không khóa ngoại, không JOIN, không connection string trỏ sang DB của service khác | PR bị từ chối |
| A3 | Cần dữ liệu của service khác → giữ **bản sao** (read model) cập nhật bằng sự kiện (ví dụ `MemberAccess`, `UserDirectory`) | |
| A4 | Service **không** gọi REST trực tiếp sang service khác. Chỉ dùng: gRPC (DiagramEngine) hoặc sự kiện Redis Streams | PR bị từ chối |
| A5 | Phát sự kiện **luôn** qua Outbox, trong cùng transaction với thay đổi nghiệp vụ. Cấm `XADD` trực tiếp trong service code | Mất / lệch dữ liệu |
| A6 | Consumer phải **idempotent**: kiểm tra Inbox theo `eventId` trước khi xử lý | Xử lý trùng |
| A7 | Chỉ Gateway mở ra ngoài cho client. DiagramEngine chỉ trong mạng nội bộ | |
| A8 | Hợp đồng chung (`Contracts.Events`, `.proto`) chỉ **thêm** field; muốn đổi/xóa → tăng `version` và báo cả nhóm | Vỡ service khác |

---

## 2. Cấu trúc 3 lớp trong mỗi service

```
<Service>.API           → Controllers, DTO request/response, Program.cs, middleware
<Service>.Service       → Interfaces + Services (nghiệp vụ), Validators, EventHandlers, Mapping
<Service>.Repositories  → Entities, DbContext, IGenericRepository + repository riêng, Migrations
```

| Lớp | Được phép | Không được phép |
|---|---|---|
| Controller | Nhận request, gọi 1 service, trả response | Chứa nghiệp vụ, dùng DbContext |
| Service | Nghiệp vụ, kiểm tra BR, gọi repository, ghi Outbox, gọi gRPC client | Trả `IActionResult`, dùng `HttpContext` |
| Repository | Truy vấn EF Core | Chứa nghiệp vụ, gọi service khác |

- API phụ thuộc Service, Service phụ thuộc Repositories. **Không** phụ thuộc ngược.
- Chỉ `DbContext` trong lớp Repositories; Service không `new` DbContext, không dùng `_context` trực tiếp.

---

## 3. Đặt tên (C# / .NET)

| Loại | Quy tắc | Ví dụ |
|---|---|---|
| Namespace / project | `UMLForge.<Service>.<Layer>` | `UMLForge.Diagram.Service` |
| Interface | Tiền tố `I` | `IDiagramService`, `IDiagramRepository` |
| Repository | `I<Entity>Repository` / `<Entity>Repository` | `IRenderJobRepository` |
| Service | `I<Domain>Service` / `<Domain>Service` | `IReviewService` |
| Method bất đồng bộ | Hậu tố `Async`, tham số cuối `CancellationToken cancellationToken = default` | `GetByIdWithProjectAsync` |
| DTO | `<Hành động><Đối tượng>Request` / `<Đối tượng>Response` | `CreateDiagramRequest`, `DiagramResponse` |
| Sự kiện | Thì quá khứ, `record` | `RenderCompleted`, `MemberAdded` |
| Handler sự kiện | `<Event>Handler` | `RenderCompletedHandler` |
| Job định kỳ (kế thừa `ScheduledJobBase`) | `<Việc>Job` | `DailyRenderReportJob` |
| Consumer sự kiện (BackgroundService) | `<Stream>Consumer` | `RenderEventsConsumer` |
| Biến private | `_camelCase` | `_diagramRepository` |
| Hằng số | `PascalCase` trong class static | `StreamNames.DiagramEvents` |

TypeScript / React:
| Loại | Quy tắc | Ví dụ |
|---|---|---|
| Component | `PascalCase.tsx` | `DiagramEditorPage.tsx` |
| Hook | `useXxx.ts` | `useRenderStatus.ts` |
| API client | `<domain>Api.ts` | `diagramApi.ts` |
| Type | `PascalCase`, không tiền tố `I` | `Diagram`, `PagedResult<T>` |

---

## 4. API

| Mục | Quy tắc |
|---|---|
| Route | `/api/<resource>` số nhiều, kebab-case: `/api/projects/{id}/members` |
| HTTP verb | GET đọc · POST tạo/hành động · PUT thay toàn bộ · PATCH sửa một phần · DELETE xóa |
| Hành động không phải CRUD | Động từ ở cuối: `POST /api/diagrams/{id}/render`, `POST /api/reviews/{id}/approve` |
| Mã trả về | 200 đọc/sửa · 201 tạo (+ `Location`) · 202 render · 204 xóa · 400 · 401 · 403 · 404 · 409 · 410 · 422 |
| Lỗi | **ProblemDetails (RFC 7807)** có `type`, `title`, `status`, `detail`, `traceId`, `errors` (validation), `code` (ví dụ `BR-14`) |
| Phân trang | Query `page` (từ 1), `pageSize` (mặc định 20, tối đa 100), `sort=field:asc`, `search`, bộ lọc riêng. Trả `PagedResult<T> { items, page, pageSize, totalItems, totalPages }` |
| Concurrency | Trả `ETag` (RowVersion base64); PUT/PATCH yêu cầu `If-Match`; sai → 409 |
| Phân quyền | `[Authorize(Roles = …)]` cho vai trò; quyền theo dữ liệu (thành viên project…) kiểm tra trong Service |
| Swagger | Mọi endpoint có `[ProducesResponseType]` cho mỗi mã trả về, có mô tả XML comment |

---

## 5. Sự kiện & gRPC

- Tên stream, consumer group, tên sự kiện lấy từ hằng số trong `Contracts`, không gõ chuỗi tay.
- Payload chỉ chứa dữ liệu nghiệp vụ cần thiết; **không** chứa mật khẩu, token, email người khác không liên quan.
- Handler phải trả về nhanh (< 5 s); việc lâu → ghi trạng thái vào DB để job định kỳ xử lý tiếp.
- Chỉ dùng Redis **Streams**. Không dùng Redis List hay Pub/Sub cho sự kiện nghiệp vụ.

## 5.1 Background Job

- Chỉ dùng **.NET BackgroundService** (không Hangfire, không Quartz).
- Job định kỳ kế thừa `ScheduledJobBase` (dùng `PeriodicTimer`), không tự viết `while + Task.Delay` riêng lẻ.
- Mỗi vòng lặp tạo **scope mới** (`IServiceScopeFactory.CreateAsyncScope()`) để lấy repository/DbContext.
- Bắt mọi exception trong một vòng: ghi log rồi chạy vòng sau, **không** để BackgroundService chết.
- Luôn truyền `stoppingToken` xuống mọi lời gọi async để tắt container sạch sẽ.
- Mỗi job định kỳ phải chạy được thủ công qua `POST /api/admin/jobs/{jobName}/run` (chỉ Admin) và ghi log kết quả.
- Lỗi tạm thời → không `XACK`, để Reconcile retry; lỗi vĩnh viễn (dữ liệu sai) → ghi log + đưa vào DLQ.
- gRPC client đăng ký bằng `AddGrpcClient<>` với deadline 5 s, retry chỉ khi `Unavailable`.
- Không đổi số thứ tự field trong `.proto`; field bỏ đi dùng `reserved`.

---

## 6. Dữ liệu & EF Core

- Mỗi thay đổi schema = 1 migration có tên rõ nghĩa: `AddReviewTable`, không `Update1`.
- Truy vấn chỉ đọc dùng `AsNoTracking()`; truy vấn danh sách luôn phân trang ở DB (`Skip/Take`), không `ToList()` rồi lọc.
- Không dùng `Include` lồng quá 2 cấp; cần nhiều hơn → viết projection `Select` ra DTO.
- Thời gian lưu UTC (`DateTime.UtcNow`), client tự đổi múi giờ.
- Xóa diagram/project là **xóa mềm** (`IsDeleted`), có global query filter.

---

## 7. Bảo mật

- Không commit secret. Dùng `.env` (đã có trong `.gitignore`) và `.env.example` chứa giá trị giả.
- Mật khẩu băm bằng BCrypt (work factor ≥ 11). Refresh token lưu **hash**, không lưu bản gốc.
- Không log: mật khẩu, JWT, refresh token, connection string, nội dung `Authorization` header.
- Validate mọi input bằng FluentValidation; giới hạn `ModelJson` ≤ 256 KB.
- SVG trả về client phải được sanitize (bỏ `<script>`, `on*=`) trước khi hiển thị bằng `dangerouslySetInnerHTML`.
- API admin (chạy job, xem báo cáo, quản lý user) chỉ cho vai trò Admin.

---

## 8. Test, comment, Git

**Test**
- Mỗi BR có ít nhất 1 unit test, tên theo dạng `MethodName_Condition_ExpectedResult` và ghi mã BR trong `[Trait("BR", "BR-14")]`.
- Mỗi sự kiện có 1 test cho handler (bao gồm trường hợp nhận trùng).
- Integration test (Testcontainers) cho 3 luồng WF-01, WF-02, WF-03.
- Không merge khi CI đỏ. Không xóa hoặc `[Skip]` test để cho CI xanh.

**Comment**
- Code tự giải thích bằng tên; chỉ comment (tiếng Việt) ở logic phức tạp: thuật toán phát hiện vòng lặp, retry/DLQ, concurrency.
- Không để code bị comment-out trong PR.

**Git**
- Nhánh: `feature/<tên>-<việc>`, `fix/<việc>`. PR vào `develop`, sau demo mới vào `main`.
- Commit theo Conventional Commits: `feat(diagram): add version history endpoint`.
- Mỗi PR ≤ ~400 dòng thay đổi, có mô tả, mã task (`H-01`, `T-03`, `M-02`, `B-04`…) và ít nhất 1 người review.
- Ai sửa vào service/task của người khác phải báo người đó trước.

---

## 9. AI tuyệt đối KHÔNG được làm (13 điều)

> Dán nguyên khối này vào đầu prompt khi nhờ AI.

```
Bạn đang làm việc trong dự án UMLForge v2 (microservices, .NET 8). Tuân thủ RULES.md. Tuyệt đối KHÔNG:
1.  Tự tạo branch, commit, push, merge hay mở PR. Chỉ đề xuất commit message.
2.  Ghi/sửa file khi chưa trình bày code và chưa được người dùng duyệt.
3.  Cài thêm package/thư viện (NuGet, npm, tool) khi chưa hỏi.
4.  Đổi hợp đồng chung: Contracts.Events, file .proto, route API, schema ProblemDetails — khi chưa được duyệt.
5.  Đọc/ghi database của service khác, thêm khóa ngoại/JOIN xuyên service, gọi REST giữa các service.
6.  XADD trực tiếp thay vì qua Outbox; viết consumer không kiểm tra Inbox.
7.  Xóa, bỏ qua ([Skip]) hoặc sửa test cho khớp code sai để CI xanh.
8.  Đưa secret (mật khẩu, JWT key, connection string thật) vào code, log hay ví dụ.
9.  Chạy lệnh phá hủy: drop database, xóa migration đã áp dụng, git reset --hard, xóa volume Docker — khi chưa hỏi.
10. Tự bịa nghiệp vụ, mã BR, số liệu khảo sát. Thiếu thông tin thì hỏi lại.
11. Sửa file ngoài phạm vi task được giao (refactor "tiện tay").
12. Sinh code dài mà không giải thích ngắn gọn nó làm gì và đã kiểm tra bằng cách nào.
13. Thêm công nghệ nhóm đã loại: Hangfire, Redis Pub/Sub, SSE/SignalR, gửi email.
```

---

## 10. Definition of Done (cho mọi task)

- [ ] Build không cảnh báo (`-warnaserror` ở CI)
- [ ] Unit test cho BR liên quan đã viết và xanh
- [ ] Swagger hiển thị đúng request/response và mã lỗi
- [ ] Đã tự chạy thử service của mình (Swagger + fake/`seed-events.ps1` theo TASKS.md mục 8); chạy qua Gateway bằng Docker Compose ở buổi ghép ngày 4
- [ ] Không sửa file ngoài thư mục mình sở hữu (TASKS.md §0.1), trừ 1 dòng đăng ký ở `App.tsx` / `AppLayout.tsx`
- [ ] Log có `CorrelationId`, không lộ secret
- [ ] Cập nhật `PROGRESS.md` (task xong, bug phát hiện)
- [ ] PR được 1 người review
