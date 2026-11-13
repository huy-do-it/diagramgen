# PROGRESS — UMLForge v2

> Cập nhật **cuối mỗi buổi làm** (hoặc khi merge PR). Ghi ngắn: xong gì, lỗi gì, làm gì tiếp.
> Khi nhờ AI làm tiếp, đưa file này cho AI đọc trước để nó biết dự án đang ở đâu.

**Cập nhật lần cuối:** 12/11/2026 — Minh triển khai và kiểm thử M-03 local
**Giai đoạn hiện tại:** PR #1/#2/#3 đã merge vào develop; baseline M-03 `64ae612`. M-03 local verified trên `feature/minh-m03-render-consumer`, chờ Minh review/Git delivery; chưa bắt đầu M-04. Kế hoạch 4 ngày, xem TASKS.md §0.2

---

## 1. Tổng quan tiến độ

| Người | Ngày 1 | Ngày 2 | Ngày 3 | Ngày 4 sáng | Xong |
|---|---|---|---|---|---|
| Huy | ✅ H-01 · ✅ H-02 | ✅ H-03 · ✅ H-04 | ✅ H-05 · ✅ H-06 | ✅ H-07 | 7/7 (Hoàn thành 100%) |
| Thiện | ⬜ T-01 · ⬜ T-02 | ⬜ T-03 · ⬜ T-04 | ⬜ T-05 · ⬜ T-06 | ⬜ T-07 | 0/7 |
| Minh | 🟨 M-01 · 🟨 M-02 | 🟨 M-03 · ⬜ M-04 | ⬜ M-05 · ⬜ M-06 | ⬜ M-07 | 0/7 |
| Bảo | ✅ B-01 · ✅ B-02 | ⬜ B-03 · ⬜ B-04 | ⬜ B-05 · ⬜ B-06 | ⬜ B-07 | 2/7 |
| Cả nhóm (ngày 4 chiều) | | | | ⬜ I-01 → I-06 | 0/6 |

Ký hiệu: ⬜ chưa làm · 🟨 đang làm · ✅ xong · ⛔ bị chặn

---

## 2. Đã xong

| Ngày | Task | Người | PR / ghi chú |
|---|---|---|---|
| 12/11/2026 | Soạn bản nháp PRD, ARCHITECTURE, DESIGN, RULES, TASKS, PROGRESS | Bảo | `docs/rebuild/` — chờ nhóm duyệt |
| 12/11/2026 | H-01 (Identity Auth, BCrypt, JWT, Refresh Token BR-01..05) & H-02 (Admin Users, Outbox Events, Purge Tokens Job) | Huy | `feature/huy-identity` — 48/48 tests PASS, build 0 warnings |
| 12/11/2026 | **H-05** Gateway YARP (:5000, 5 clusters, rate limit, CORS, CorrelationId, Docker) | Huy | 0 errors, 0 warnings build, gateway.yml kích hoạt |
| 12/11/2026 | **H-06** Client UI (Login UI-01, Register UI-02, Admin Users UI-10, Projects UI-03, Detail UI-04, Invite UI-05, Invitations) | Huy | TypeScript build clean 0 errors, Vite production build clean |
| 12/11/2026 | **H-07** Packaging, Docker Compose, E2E Verification Huy (Identity, Workspace, Gateway, Client UI) | Huy | Hoàn tất toàn bộ 7/7 task của Huy |
| 12/11/2026 | **H-03/H-04** Workspace Service (Project, Members, Invitations, UserDirectory, ExpireInvitationsJob, BR-06..10) | Huy | 30/30 tests PASS (25 BR tests), build 0 warnings/errors, Outbox/Inbox integrated |
| 12/11/2026 | Tinh gọn phạm vi: bỏ Hangfire, realtime (Pub/Sub + SSE), email; giữ thông báo trong app | Bảo | Xem mục 2.1 |
| 12/11/2026 | **B-01** Khung repo + Contracts (16 sự kiện, proto 4 RPC) | Bảo | `aca973f` |
| 12/11/2026 | **B-01** BuildingBlocks Auth + Web + Jobs | Bảo | `83667c4` — kiểm bằng API mẫu: ProblemDetails, JWT, chạy job tay |
| 12/11/2026 | **B-01/B-02** BuildingBlocks Outbox + EventBus (Redis Streams, Inbox, retry, DLQ) | Bảo | `9f65a86` — kiểm 9/9 kịch bản với Redis 7 thật |
| 12/11/2026 | **B-01** `make-dev-token.ps1`, khung client v2 (proxy Vite, `shared/api.ts`, auth, layout, `routes.tsx` mẫu), compose `include` + 9 file mẫu | Bảo | `8dcf665` — kiểm build client, proxy, đăng nhập/đăng xuất trên trình duyệt, `docker compose config` |
| 12/11/2026 | **B-02** Integration test Testcontainers (Redis 7 + SQL Server 2022): 7 test EventBus/Outbox + 4 test ScheduledJob | Bảo | 11/11 xanh, chạy 3 lần ổn định; cần Docker khi chạy `dotnet test` |
| 12/11/2026 | M-01 final source review + Git delivery | Minh | Commit `590674c`, [PR #1](https://github.com/huy-do-it/diagramgen/pull/1) vào develop, MERGED tại 5b7dc6c. Fresh build0warning/error, unit43/43 và SQL/PlantUML10/10 PASS; chưa nhận Done toàn DoD. |
| 12/11/2026 | M-02 Notification DB/API review + delivery | Minh | Commit1d414f1, [PR #2](https://github.com/huy-do-it/diagramgen/pull/2) MERGED vào develop tại5ebea98; unit19/19 và44real API/SQL assertions PASS. Resolve PR2: Render43/43 + Notification19/19 PASS; chưa nhận acceptance tích hợp B-01/Identity/Gateway. |

### 2.1 Quyết định đã chốt

| # | Ngày | Quyết định | Lý do |
|---|---|---|---|
| D-01 | 12/11/2026 | Kiến trúc **microservices**, mỗi service một database | Yêu cầu của nhóm khi xây lại |
| D-02 | 12/11/2026 | Background Job chỉ dùng **.NET BackgroundService** (như v1), **không Hangfire** | Đề ghi rõ BackgroundService; tái sử dụng code v1 |
| D-03 | 12/11/2026 | Message broker chỉ dùng **Redis Streams**, **không Pub/Sub**, không Redis List | Streams đủ tiêu chí 15%, không mất message, nhiều consumer |
| D-04 | 12/11/2026 | **Bỏ realtime**: client polling 2 s | Không phải tiêu chí chấm |
| D-05 | 12/11/2026 | **Bỏ email**, giữ **thông báo trong app** (Notification service) | Không phải tiêu chí chấm; vẫn có service thứ 5 tiêu thụ sự kiện |

### 2.2 Lưu ý kỹ thuật từ B-01 (mọi người đọc trước khi dùng gói chung)

| # | Lưu ý | Chi tiết |
|---|---|---|
| N-01 | Ghim SDK .NET 8 | `global.json` ở gốc repo → `dotnet new` tạo `net8.0` + `.sln` (máy có SDK 9/10 cũng vậy) |
| N-02 | EF Core đúng bản **8.0.33** | BuildingBlocks dùng 8.0.33; ghim bản thấp hơn sẽ lỗi NU1605 |
| N-03 | Cấu hình Redis | `ConnectionStrings:Redis` (máy: `localhost:6379`; compose: `ConnectionStrings__Redis: ${REDIS_URL}`) |
| N-04 | Handler sự kiện không gọi `SaveChanges` | EventConsumer lưu 1 lần gồm thay đổi của handler + Inbox |
| N-05 | Consumer group tạo từ đầu stream (`0-0`), không dùng `$` | Không mất sự kiện phát trước khi service chạy lần đầu (ví dụ `UserRegistered` lúc seed); Inbox chặn trùng |
| N-06 | EventConsumer tự retry | Cứ 30 s nhận lại message treo > 60 s; giao ≥ 5 lần vẫn lỗi → `<stream>.dlq`. ReconcileJob của Render vẫn dùng `IStreamMaintenance` |
| N-07 | Dockerfile cài `curl` | Image `aspnet:8.0` không có `curl` → healthcheck `/health` cần `apt-get install curl` |
| N-08 | Compose: build context `../../src` | Đường dẫn trong `deploy/compose/*.yml` tính từ thư mục `deploy/compose/` |
| N-09 | Project `webapi` mới: xóa `app.UseHttpsRedirection()` | Container chỉ chạy HTTP; `AddUmlForgeDefaults` + `UseUmlForgeDefaults` đã đủ middleware |
| N-10 | Client: chỉ sửa `features/<tên>/routes.tsx` | `App.tsx` đã gắn sẵn route của cả 6 feature; gọi API qua `shared/api.ts` (`apiRequest`, `apiFetch` khi cần `ETag`) |

---

## 3. Đang làm

| Task | Người | Bắt đầu | Ghi chú |
|---|---|---|---|
| Chốt tài liệu | Cả nhóm | — | Đọc TASKS.md mục 1 (hợp đồng chung) và trả lời câu hỏi mở ở mục 6 trước ngày 1 |
| M-01 review/integration | Minh | 12/11/2026 | PR #1 đã merge tại 5b7dc6c. Đối chiếu6docs PASS; sửa UTC Kind SQL readback. Adapter controlled gRPC7/7 và host4/4 PASS; B-01/B-04 team integration NOT RUN. Không có CI workflow trên baseline, không nhận local PASS là CI xanh. |
| M-02 integration (đã merge PR #2) | Minh | 12/11/2026 | Branch `feature/minh-m02-notification-api` độc lập từ develop `0e86974`; Minh nghiệm thu và giao Git delivery. Source khớp manifest29/29 trước thay checkpoint docs. Release0warning/error,19unit PASS/0skip và44real API/SQL assertions PASS. B-01/Identity/Gateway acceptance còn chờ. |
| M-03 local verified | Minh | 12/11/2026 | Dùng Contracts/BuildingBlocks thật sau PR #3. Release 0 warning/error, 54/54 unit và 24/24 SQL/Redis/PlantUML checks PASS; chờ Minh review, chưa commit/push/PR. B-04 semantics/Diagram/Gateway/full Compose chưa nghiệm thu. |

### M-03 — contract và evidence hiện tại

- Minh đã duyệt durable handoff: handler chỉ thêm Queued; shared dispatcher commit job + Inbox rồi ACK theo N-04. Processor riêng commit Running + Started Outbox trước pipeline; commit Done/artifact/Completed hoặc Failed/Failed Outbox cùng transaction sau pipeline. Đây là điều chỉnh so với pseudocode inline handler ở TASKS M-03; ACK xác nhận intake bền vững, không phải render hoàn tất.
- Unique diagram/version chặn intake trùng; Status + Attempt concurrency tokens chặn claim đôi và stale writer. CorrelationId được lưu, khôi phục cho gRPC/log/Outbox. Không sửa source/shared contracts của Bảo hoặc module đồng đội.
- 24 checks dùng SQL Server 2022, Redis Streams 7, HTTP PlantUML thật, hai Worker processes và controlled Engine: pending replay/intake rollback, restart Queued sau ACK, duplicate/race, atomic claim/final writes, Redis outage/outbox recovery, timeout/DLQ, artifact cũ, shutdown, migration nâng M-01 và gRPC metadata. SDK 8.0.428/runtime 8.0.34 riêng; 54 unit PASS/0 skip.
- Running gián đoạn/lỗi hạ tầng sau claim được giữ bền vững; **recovery thuộc M-04**, chưa tự phục hồi Running. Không nhận exactly-once transport hoặc toàn bộ WF acceptance. Shared seed-events chưa có; harness ngoài repo dùng canonical envelope thay cho bước manual seed, không thêm tool thay Bảo.
- Evidence local ngoài repo: `../assignment-planning/v2-m03-evidence/`; contract/chạy service tại `src/Services/Render/README.md`. Lần harness đầu lỗi compile và một lượt wait-condition chưa ổn định đã sửa trong harness; kết quả cuối 24/24, không sửa product để né kiểm tra.

### Minh — checkpoint trước PR #3 (lịch sử, giữ bằng chứng)

- Minh cho phép tạo develop từ main `0e86974`, mở PR M-01 vào develop theo RULES; không merge/main changes. M-02 ban đầu độc lập từ develop0e86974; đã merge develop5b7dc6c để đồng bộ M-01 và xử lý conflict PROGRESS, không đổi source service. M-01 có35file mới Render + checkpoint PROGRESS; M-02 chỉ Notification + checkpoint PROGRESS.
- M-02 áp dụng BR-33 ở service và owner filter trong SQL; read/read-all conditional update không làm mới ReadAt khi gọi lặp/đồng thời. Seed chỉ Development + Admin, tạo5thông báo cho chính Admin. Timestamps UTC; ProblemDetails có traceId/code/errors; CorrelationId giữ cả khi exception.
- B-01 chưa publish Web/Auth. M-02 bootstrap JWT Bearer/ProblemDetails/Swagger trực tiếp trong API theo contract TASKS, không tạo shared library thay Bảo hoặc phát token. JSON page đúng shape PagedResult. Khi B-01 có cần thay wiring sang shared defaults và kiểm tra lại; chưa nhận acceptance B-01/Identity/Gateway.
- Runtime test .NET8.0.32 riêng; SQL2022 container fixture không mount dữ liệu v1. JWT fixture và runners ngoài source, không secret trong appsettings. Evidence workspace `assignment-planning/v2-m01-evidence/` và `v2-m02-evidence/`; contract/chạy M-02 tại `src/Services/Notification/README.md`.
- M-03/M-04 còn cần thống nhất Inbox/ACK transaction, handler<5s vs pipeline30s, pending replay, interrupted existing job, ownership khi hai Worker và retry/event ordering. M-05 còn cần thống nhất actor/requestedBy và recipient ordering. Chưa tự đổi shared contract hoặc bắt đầu các task này.

### M-01 — checkpoint trước PR #1 (lịch sử, giữ bằng chứng)

- Chỉ source trong `src/Services/Render/`; pipeline trả outcome, M-03 sẽ commit job/artifact/Outbox cùng transaction. Không có Streams/consumer/recovery/admin API hoặc Notification implementation.
- B-01 hiện chưa có shared project/BuildingBlocks; không tạo Contracts thay Bảo. Adapter compile khi project contract chuẩn tồn tại; thiếu contract thì real mode từ chối startup, Development phải chủ động bật fake.
- SQL/PlantUML thật trong fixture riêng; Engine fake. Adapter được build/chạy qua HTTP/2 với proto/server fixture theo TASKS ngoài repo; không nhận là kiểm thử Engine của nhóm. Host smoke 4/4 PASS (SQL health 200/503, Production fake rejection, missing-contract rejection). Team gRPC/Streams/Gateway/browser NOT RUN.
- Lượt test đầu bị aborted vì Core runtime thiếu patch tương ứng ASP.NET; final tests dùng runtime riêng .NET 8.0.32. Không sửa runtime hệ thống. Chi tiết tại `src/Services/Render/README.md`; evidence ngoài repo ở workspace `assignment-planning/v2-m01-evidence/`.
- M-03/M-04 cần chốt lại transaction Inbox/ACK, pending replay, job đã tồn tại nhưng gián đoạn, quyền ghi khi hai Worker/retry và event ordering với Bảo/Thiện. Chưa tự đổi contract chung.
- Git workflow: Minh cho phép tạo `develop` từ main `0e86974` vì remote ban đầu chỉ có main; PR M-01 nhắm develop theo RULES. Không đổi main, không thêm CI thay Bảo.

---

## 4. Bug / vấn đề đã biết

| # | Mô tả | Mức độ | Service | Trạng thái | Ghi chú |
|---|---|---|---|---|---|
| — | Chưa ghi nhận bug sản phẩm đang mở trong phạm vi M-03 đã kiểm thử; Running recovery còn thuộc M-04 | | Render | Pending M-04/integration | PR #3 đã merge; B-04 semantics và toàn hệ thống chưa nghiệm thu. |

**Rủi ro kỹ thuật đã biết trước (từ v1 và thiết kế):**

| # | Rủi ro | Cách giảm |
|---|---|---|
| R1 | StackExchange.Redis không hỗ trợ `XREADGROUP BLOCK` → phải poll | Poll 200 ms, `COUNT 10`; đo CPU khi demo |
| R2 | Worker xử lý quá nhanh làm test "render trùng → 409" không ổn định (đã gặp ở v1) | Test BR-14 ở tầng Service với mock, không phụ thuộc thời gian thực |
| R3 | 11 container tốn 4–5 GB RAM, máy yếu chạy chậm | `docker-compose.override.yml` giới hạn RAM SQL Server; có video dự phòng |
| R4 | Dữ liệu bản sao (`MemberAccess`) trễ vài trăm ms sau khi thêm thành viên | UI hiển thị "đang đồng bộ"; tài liệu ghi rõ eventual consistency |
| R5 | Đổi hợp đồng sự kiện làm vỡ service khác | RULES A8: chỉ thêm field, tăng `version` |
| R6 | Nhóm 4 người, 6 service — khối lượng lớn | Danh sách cắt giảm ở cuối TASKS.md |

---

## 5. Tái sử dụng từ v1 (`PRN232-DiagramGen`)

| Phần v1 | Vị trí v1 | Dùng lại ở v2 | Mức sửa |
|---|---|---|---|
| Sinh PlantUML 5 loại sơ đồ (alt/opt/loop, DFD context, include/extend…) | `PRN232.DiagramGen.Services/Services/PlantUmlGenerator.cs` | DiagramEngine (B-03) | Thấp — đổi namespace |
| Validate model + luật UML (vòng lặp, nhóm liên thông, trùng tên…) | `Services/Validators/DiagramValidators.cs` | DiagramEngine (B-03) | Thấp |
| File proto + gRPC server | `PRN232.DiagramGen.GrpcService/Protos/diagram_engine.proto`, `Services/DiagramEngineServiceImpl.cs` | Contracts.Grpc + DiagramEngine (B-01, B-04); chạy tạm thay DiagramEngine cho Thiện | Trung bình — thêm 2 RPC, giữ nguyên package/service |
| Pipeline render, Reconcile, Cleanup | `PRN232.DiagramGen.Worker/` (`IRenderPipeline`, `ReconcileHostedService`, `CleanupHostedService`) | Render (M-01, M-04) | Trung bình — đổi nguồn từ Redis List sang Streams, viết lại thành `ScheduledJobBase` |
| Repository pattern (Generic + riêng từng entity) | `PRN232.DiagramGen.Repositories/` | Mỗi service (H-01, H-03, T-01, M-01, M-02) | Thấp — tách theo DB |
| Editor JSON, template, example panel, xử lý chỗ trống | `client/src/pages/DiagramEditorPage.tsx`, `templates/diagramTemplates.ts`, `components/DiagramExamplePanel.tsx`, `utils/diagramJson.ts` | Client UI-06 (B-06) | Thấp |
| Trang xuất | `client/src/pages/DiagramExportPage.tsx`, `ExportPage.tsx` | UI-07 / T-06 ➕ | Trung bình |
| Design token | `client/src/styles.css` | `tokens.css` (H-06) | Thấp |
| Unit test (~270 test) | `tests/PRN232.DiagramGen.UnitTests/` | Test generator/validator chuyển nguyên; test service viết lại | — |

**Không dùng lại:** DbContext chung `UMLForgeDb`, hàng đợi Redis List `diagram.render.jobs`, các service gọi trực tiếp nhau.

---

## 6. Câu hỏi mở / quyết định cần chốt

| # | Câu hỏi | Người quyết | Trả lời |
|---|---|---|---|
| Q1 | Deadline nộp và ngày thuyết trình? Kế hoạch đang giả định 4 ngày code + thời gian làm slide riêng. | Cả nhóm | |
| Q2 | Cả nhóm có đồng ý xây lại không, hay chỉ sửa v1 (đổi Redis List sang Streams)? | Cả nhóm | |
| Q3 | Có làm vai trò Lecturer + Review (NV-06) không? | Cả nhóm | |
| Q4 | Có thêm AI (`TextToModel` – Gemini) không? | Cả nhóm | |
| Q5 | Deploy cloud hay chỉ chạy Docker Compose local? | Cả nhóm | |
| Q6 | Repo mới hay làm tiếp trong repo hiện tại (nhánh riêng)? | Cả nhóm | |
| Q7 | Phân công trong TASKS.md có đúng với Google Sheet không? | Leader | |
| Q8 | Tài liệu lệch nhau: proto (ARCHITECTURE §7 vs TASKS §1.7), consumer group Render (`render-workers` vs `render-svc`), route chạy job (RULES §5.1 `/api/admin/jobs/...` vs TASKS `/api/admin/<svc>/jobs/...`). Code đang theo TASKS §1 — có sửa ARCHITECTURE/RULES cho khớp không? | Cả nhóm | |
| Q9 | Repo chưa có nhánh `develop` (RULES §8 yêu cầu PR vào `develop`) — ai tạo? | Leader | Đã giải quyết: Minh được cho phép tạo develop từ main0e86974; PR #1/#2 đã merge vào develop. |

---

## 7. Việc tiếp theo

1. Gửi 6 file trong `docs/rebuild/` cho nhóm đọc; họp 30 phút chốt mục 6 và **TASKS.md mục 1 (hợp đồng chung)**.
2. Ngày 1, 8:00: Bảo làm B-01 (xong trước 11:00); 3 người còn lại bắt đầu H-01, T-01, M-01 ngay.
3. Cuối mỗi ngày: mỗi người tick ô của mình ở mục 1 và ghi nhật ký.
4. Cập nhật Google Sheet theo TASKS.md đã chốt.

---

## 8. Nhật ký

### 12/11/2026 — Huy
- Xong: H-03 (Project & Member Management, soft-delete, ETag concurrency, BR-06..08) & H-04 (Invitations, UserDirectory sync, ExpireInvitationsJob, BR-09..10).
- Test: 30/30 tests PASS (25 BR tests covering BR-06, BR-07, BR-08, BR-09, BR-10, BR-16; 4 directory sync idempotency tests; 1 background expiration job test).
- Build: `dotnet build src/Services/Workspace/Workspace.sln --warnaserror` 0 warnings, 0 errors.
- Compose: Kích hoạt `deploy/compose/workspace.yml` (port 5002, WorkspaceDb, Redis).
- Bug mới: không.
- Bị chặn bởi: không.
- Tiếp theo: H-05 Frontend Workspace Feature (Project list, member management, invitation UI).

### 12/11/2026 — Bảo
- Xong: B-01 (4 commit: khung repo + Contracts · Auth/Web/Jobs · Outbox/EventBus · tools/client/compose). Phần code chính của B-02 (Outbox, EventBus, ScheduledJobBase) cũng đã nằm trong B-01.
- Xong: B-02 (integration test Testcontainers, 11/11 xanh).
- Bug mới: không. Lệch tài liệu → mục 6 Q8.
- Bị chặn bởi: không.
- Tiếp theo: B-03 DiagramEngine (chép generator/validator v1).

---

## Mẫu ghi nhật ký (copy cho mỗi buổi)

```
### dd/MM/yyyy — <Tên>
- Xong: H-01 (PR #..)
- Đang làm: H-02 (~60%)
- Bug mới: <mô tả> → ghi vào mục 4
- Bị chặn bởi: (nếu có — ghi task + người)
- Tiếp theo: …
```
