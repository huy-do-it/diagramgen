# PROGRESS — UMLForge v2

> Cập nhật **cuối mỗi buổi làm** (hoặc khi merge PR). Ghi ngắn: xong gì, lỗi gì, làm gì tiếp.
> Khi nhờ AI làm tiếp, đưa file này cho AI đọc trước để nó biết dự án đang ở đâu.

**Cập nhật lần cuối:** 12/11/2026 — đối chiếu và cập nhật bàn giao M-01–M-07 của Minh.
**Giai đoạn hiện tại:** M-01–M-07 đã triển khai, kiểm thử trong phạm vi Minh và merge vào `develop`; M-07 qua PR #10, merge commit `761f0ed`. Snapshot kiểm tra ghép nhóm: `81badf9` (đã gồm PR #11 Diagram và PR #12 Workspace). Đang chuẩn bị tích hợp I-01–I-06; chưa nghiệm thu toàn bộ hệ thống. Xem TASKS.md §7 và các giới hạn bên dưới.

---

## 1. Tổng quan tiến độ

| Người | Ngày 1 | Ngày 2 | Ngày 3 | Ngày 4 sáng | Xong |
|---|---|---|---|---|---|
| Huy | ✅ H-01 · ✅ H-02 | ✅ H-03 · ✅ H-04 | ✅ H-05 · ✅ H-06 | ✅ H-07 | 7/7 (Hoàn thành 100%) |
| Thiện | ✅ T-01 · ✅ T-02 | ✅ T-03 · ✅ T-04 | ✅ T-05 · ✅ T-06 | ✅ T-07 | 7/7 (Hoàn thành 100%) |
| Minh | ✅ M-01 · ✅ M-02 | ✅ M-03 · ✅ M-04 | ✅ M-05 · ✅ M-06 | ✅ M-07 | 7/7 (đã bàn giao; chờ nghiệm thu tích hợp chung) |
| Bảo | ✅ B-01 · ✅ B-02 | ✅ B-03 · ✅ B-04 | ✅ B-05 · ✅ B-06 | ✅ B-07 | 7/7 — đóng theo nghiệm thu leader, Minh xác nhận 14/11/2026 |
| Cả nhóm (ngày 4 chiều) | | | | ⬜ I-01 → I-06 | 0/6 |

Ký hiệu: ⬜ chưa làm · 🟨 đang làm · ✅ xong · ⛔ bị chặn

Đợt cập nhật này xác minh phần Minh bằng source, PR đã merge và evidence. Các dòng của đồng đội được giữ nguyên; bảng không thay thế nghiệm thu liên service. Dấu ✅ của Minh xác nhận deliverables từng task đã bàn giao, không xác nhận CI, review độc lập hoặc toàn bộ WF-01–03 đã đạt.

---

## 2. Đã xong

| Ngày | Task | Người | PR / ghi chú |
|---|---|---|---|
| 12/11/2026 | T-07 Giao diện React Diagram & Hoàn thiện | Thiện | UI-07 (DiagramViewPage), UI-08 (ReviewPanel), DiagramListPanel, useRenderStatus hook polling 2s, Dockerfile multi-stage, deploy/compose/diagram.yml, Subagent QA test pass 100% |
| 12/11/2026 | T-06 Review + Bình luận + Xuất ZIP | Thiện | Review & Comment entities, POST /api/reviews (BR-29), approve/request-changes (BR-30), needsResubmit (BR-31), comments (BR-32), export ZIP, Subagent QA test pass 100% |
| 12/11/2026 | T-05 gRPC DiagramEngine Client | Thiện | IDiagramEngineClient, DiagramEngineClient, FakeDiagramEngineClient, UML validation (BR-18..25), HTTP 422 BR-UML, validate/suggestions/validate-project APIs, Subagent QA test pass 100% |
| 12/11/2026 | T-04 Consumers: workspace & render | Thiện | InboxMessages idempotency, WorkspaceEventsConsumer, RenderEventsConsumer (BR-28), Subagent QA test pass 100% |
| 12/11/2026 | T-03 Render request + Outbox | Thiện | OutboxMessages entity & migration, POST /api/diagrams/{id}/render (BR-14, BR-32, BR-11), OutboxPublisher BackgroundService, Subagent QA test pass 100% |
| 12/11/2026 | T-02 Phiên bản + concurrency | Thiện | DiagramVersion entity & repo, BR-15, BR-16 (ETag If-Match), BR-17, RestoreVersion API, Subagent QA test pass 100% |
| 12/11/2026 | T-01 Diagram: DB + CRUD | Thiện | Đã tạo Diagram.sln, 4 projects, EF Core migration, Subagent QA test pass 100% |
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
| 12/11/2026 | **B-02** Integration test Testcontainers (Redis 7 + SQL Server 2022): 7 test EventBus/Outbox + 4 test ScheduledJob | Bảo | `c717cfa` — 11/11 xanh, chạy 3 lần ổn định; cần Docker khi chạy `dotnet test` |
| 12/11/2026 | **B-03** DiagramEngine: chép model, parser, `PlantUmlGenerator` v1; tách luật UML (BR-18…25) + gợi ý quan hệ sang `Validators/`; chép test v1 | Bảo | `952e1bc` — 53/53 test v1 xanh; service chạy :5006 (h2c). Không chép `DiagramValidators.cs` v1 (validator DTO → thuộc Diagram service) |
| 12/11/2026 | **B-04** DiagramEngine: 4 RPC (`GeneratePlantUml` validate trước → `InvalidArgument`; `ValidateProject` server streaming), interceptor log + `x-correlation-id` (trả lại trong trailer), gRPC health, reflection (Development) | Bảo | `1ab1ce4` — 65/65 test (12 test gRPC end-to-end); gọi thật qua h2c :5006 đúng cả 4 RPC + health |
| 12/11/2026 | **B-05** Compose: `infra.yml` (sqlserver, redis AOF, plantuml), `engine.yml` (Dockerfile + healthcheck gRPC), `client.yml` (nginx, `/api` → gateway); project `umlforge-v2`, network `umlforge`; CI GitHub Actions (matrix tự dò mọi `.sln` + client + compose config) | Bảo | `0367b08` — `docker compose up` thật: 5 container healthy, kiểm SQL/Redis/PlantUML SVG/gRPC/client; CI chưa chạy trên GitHub (chỉ kiểm cú pháp + lệnh tương đương ở máy) |
| 12/11/2026 | **B-06** Editor UI-06 (`features/editor`: chuyển `DiagramEditorPage`, example panel, template CampusEats, `diagramJson` từ v1; API v2: Guid, `If-Match`/ETag, 409 BR-13/14/16, 422 lỗi UML, cảnh báo, gợi ý quan hệ) + hook `shared/useRenderStatus.ts` (polling 2 s khi Pending/Rendering) | Bảo | Chạy trên trình duyệt với API giả theo hợp đồng TASKS: tạo/lưu/render tự cập nhật, 409 + Tải lại, 422, render lỗi giữ SVG cũ, SVG có `<script>` không chạy. **Chưa test với Diagram API thật** (xem Q10) |
| 12/11/2026 | **B-07** `tools/seed-events.ps1` + 22 envelope mẫu CampusEats (`tools/events/*`, 12/16 loại — bỏ khóa user/xóa project để không phá demo), `tools/reset-demo.ps1` (xác nhận → xóa volume v2 → build → chờ healthy → `-Seed`), test `SampleEventFilesTests` (file mẫu khớp Contracts), `README.md` | Bảo | `23eec57` (B-06). seed chạy được trên PS 7 và PS 5.1; reset-demo chạy thật 5 container healthy, volume v1 không bị đụng; BuildingBlocks 35/35 test. Phần "tạo dữ liệu CampusEats qua Gateway" chuyển sang I-01 (API chưa có) |
| 12/11/2026 | M-01 final source review + Git delivery | Minh | Commit `590674c`, [PR #1](https://github.com/huy-do-it/diagramgen/pull/1) vào develop, MERGED tại 5b7dc6c. Fresh build0warning/error, unit43/43 và SQL/PlantUML10/10 PASS; chưa nhận Done toàn DoD. |
| 12/11/2026 | M-02 Notification DB/API review + delivery | Minh | Commit1d414f1, [PR #2](https://github.com/huy-do-it/diagramgen/pull/2) MERGED vào develop tại5ebea98; unit19/19 và44real API/SQL assertions PASS. Resolve PR2: Render43/43 + Notification19/19 PASS; chưa nhận acceptance tích hợp B-01/Identity/Gateway. |
| 12/11/2026 | M-03 Consumer render + Inbox/Outbox | Minh | [PR #4](https://github.com/huy-do-it/diagramgen/pull/4) MERGED tại `a22b7f0`. Durable Queued handoff, atomic claim, Started/Completed/Failed, correlation; 24 SQL/Redis/PlantUML checks PASS, gồm hai Worker, duplicate/race/rollback/restart. Engine controlled, chưa nhận B-04 semantics. |
| 12/11/2026 | M-04 Reconcile, cleanup, báo cáo + API admin | Minh | [PR #5](https://github.com/huy-do-it/diagramgen/pull/5) MERGED tại `b0eb16a`. Recovery Running quá 5 phút, retry từ snapshot, cleanup bảo vệ cohort chưa có report; 29 regression checks + 11 process restart/recovery checks PASS. |
| 12/11/2026 | M-05 Notification consumers + purge | Minh | [PR #6](https://github.com/huy-do-it/diagramgen/pull/6) MERGED tại `ded2942`. Ba stream, 11 loại event, ProjectRecipients/Inbox, recipient đã chốt, purge 6 giờ; 39 unit + 29 SQL/Redis/API checks PASS tại revision nghiệm thu. |
| 12/11/2026 | M-06 UI-09, UI-11 + NotificationBell | Minh | [PR #8](https://github.com/huy-do-it/diagramgen/pull/8) MERGED tại `fffcff3`. Bell 30 giây/dropdown 10, notifications, jobs/retry/reports và job cards 5 service. Build PASS; 31 controlled-browser + 9 live Render/Notification API browser checks + 2 SQL assertions PASS. Layout chung chờ H-06 theo Minh duyệt. |
| 12/11/2026 | M-07 Unit tests + Docker/Compose | Minh | Commit `d6ad67a`, [PR #10](https://github.com/huy-do-it/diagramgen/pull/10) MERGED tại `761f0ed`. Đúng 8 file: hai Dockerfile, hai ignore, hai Compose và hai file test. 84 Render + 41 Notification unit PASS, 0 FAIL/SKIP; hai Linux image và 13 nhóm container checks PASS; Production/UseFake=false, SQL/Redis/PlantUML thật, gRPC fixture theo proto. |

Số test ở mỗi mốc có phần tích lũy/trùng phạm vi, không cộng thành một tổng. Regression mới trên `81badf9`: Identity 48, Workspace 30, Diagram 68, Render 84, Notification 41 = **271 unit PASS, 0 FAIL/SKIP**, Release `/warnaserror`; frontend TypeScript/Vite build PASS. Kết quả này không chứng minh contract liên service hoặc acceptance toàn WF.

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
| N-11 | Compose project tên **`umlforge-v2`** | Tránh trùng container/volume với project `umlforge` của v1 còn trên máy. Chạy riêng hạ tầng: `docker compose -p umlforge-v2 -f deploy/compose/infra.yml --env-file deploy/.env up -d` |
| N-12 | Gọi DiagramEngine khi chạy service bằng `dotnet run` | `DiagramEngine:Url = http://localhost:5006` (cổng chỉ mở cho 127.0.0.1); trong Docker: `http://diagram-engine:8080` |
| N-13 | `src/.dockerignore` dùng chung | Mọi Dockerfile .NET build với context `src/` — không cần tự viết `.dockerignore` |
| N-14 | Theo dõi render ở UI: `useRenderStatus(diagramId, initialSnapshot)` | `client/src/shared/useRenderStatus.ts` → `{ status, svg, error, pollError, isRendering, track, refresh, snapshot }`; gọi `track()` ngay sau `POST /render` 202; `snapshot.etag` là ETag mới nhất cho `If-Match` |

---

## 3. Đang làm

| Task | Người | Bắt đầu | Ghi chú |
|---|---|---|---|
| Chốt tài liệu | Cả nhóm | — | Đọc TASKS.md mục 1 (hợp đồng chung) và trả lời câu hỏi mở ở mục 6 trước ngày 1 |
| Chuẩn bị I-04/I-06 và kiểm chứng phần Minh khi ghép | Minh + cả nhóm | 12/11/2026 | M-01–M-07 đã merge. Còn nối producer Diagram/Engine thật/Gateway, kiểm restart/retry ordering, recipient khi membership thay đổi và UI-11 job contract của các service. Chưa bắt đầu nghiệm thu toàn WF. |

### Minh — trạng thái bàn giao và giới hạn hiện tại

- M-03 giữ durable handoff đã duyệt: Queued + Inbox commit trước ACK; processor commit Running + Started trước pipeline, rồi commit kết quả/artifact + Outbox nguyên tử. ACK chỉ xác nhận intake bền vững.
- M-04 đã khép recovery Running, tách bảo trì Redis khỏi xử lý SQL, đưa chính sách nghiệp vụ về service và kiểm cleanup–report. Báo cáo theo ngày CreatedAt UTC; hoãn cohort còn active và tìm lại; report đã chốt bất biến. Admin retry xếp Queued, Attempt tăng khi processor claim.
- M-05: kết quả render gửi `requestedBy` kể cả actor; sự kiện cộng tác loại actor. Purge chỉ xóa IsRead với ReadAt quá 30 ngày. Notification đã nối shared Web/Auth qua [PR #15](https://github.com/huy-do-it/diagramgen/pull/15), merge `0fcc501`; giữ HS256/Guid-sub, validation400, SQL health và seed. 46 unit + 57 SQL/Redis/API checks PASS; chưa nghiệm thu Gateway/full stack.
- M-06 đã đối chiếu DESIGN trong hai feature. Sidebar/topbar, icon/drawer và tokens chung chờ H-06 theo quyết định Minh; không nhận toàn UI đạt 100%.
- M-07 build/smoke dùng image thật; gRPC fixture chỉ chứng minh proto/transport/correlation, không chứng minh thuật toán Engine của Bảo. 11 loại event Notification có log Inbox bỏ qua bản replay. Fixture đã dọn, không thay dữ liệu chung.
- Tại mốc M-07 chưa có CI; PR #14 của Bảo đã bổ sung workflow matrix. Chỉ nhận CI xanh khi có run thành công trên GitHub, không thay bằng local PASS. PR #10 không có review độc lập được ghi nhận khi merge. Toàn stack, Testcontainers WF-01–03 và chỉ tiêu p95 chưa nghiệm thu.
- Evidence local **ngoài repository**: workspace `assignment-planning/v2-m01-evidence/` đến `v2-m07-evidence/`; báo cáo `v2-m07-evidence/FINAL_HANDOVER.md`. Audit ghép tại `assignment-planning/v2-integration-readiness/READINESS_REPORT.md`; các file này không tự được upload kèm PR.

### M-03 — checkpoint trước bàn giao PR #4 (lịch sử)

Các dòng dưới ghi đúng trạng thái tại mốc M-03. Những câu “chờ review/chưa có M-04” đã được thay thế bởi trạng thái bàn giao ở trên; giữ lại để truy vết evidence.

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
| INT-M01 | Diagram phát RenderRequested.DiagramType dạng chuỗi, shared contract của Render yêu cầu int 1..5 | Chặn WF-02 | Diagram → Render | Đã sửa | Thiện đã sửa phát int 1..5 khớp shared contract; 65/65 unit tests PASS. |
| INT-M02 | Cùng diagram/version qua nhiều lần retry chưa có attempt sequence trong event; recipient read model đa stream có độ trễ | Cần kiểm khi ghép | Render / Diagram / Notification | Mở | Không tự đổi shared contract; kiểm kết quả attempt cũ và membership thay đổi đồng thời. |
| INT-M03 | Chưa đủ đường chạy Engine/Gateway/Compose và API job chung cho UI-11 | Chặn nghiệm thu chung | Toàn hệ thống | Chờ phần publish/tích hợp của nhóm | Root Compose config từng lỗi tại `81badf9`; với phần hạ tầng PR #14 đã PASS config, Engine thật đã có. Gateway và Identity GET job list/response chuẩn vẫn cần ghép. Không suy ra 11 container/WF acceptance từ config PASS. |

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
| Q10 | Editor (B-06) đang theo hợp đồng suy ra từ TASKS T-01…T-05, cần Thiện xác nhận: (1) `GET /api/diagrams/{id}` trả `id, projectId, type` (chuỗi `"UseCase"`)`, title, modelJson, status` (chuỗi)`, currentVersion, svgXml, plantUml, renderedAt, lastError, createdAt, updatedAt` + header `ETag`; (2) 409 có `code` `BR-13`/`BR-14`/`BR-16`; (3) POST/PUT trả thêm `warnings[]`, `needsResubmit`; (4) lỗi UML khi lưu → 422 `errors.modelJson[]`; (5) `POST /api/diagrams/{id}/validate` body `{ modelJson }`; (6) route xem sơ đồ UI-07 là `/diagrams/{id}` | Thiện + Bảo | |
| Q11 | PRD gộp "BR-18…BR-25 = luật UML" nhưng không ghi BR nào ứng với luật nào; RULES §8 yêu cầu `[Trait("BR", …)]` cho mỗi test. Đề xuất theo thứ tự trong PRD §7: BR-18 include/extend · BR-19 generalization · BR-20 vòng lặp · BR-21 use case/actor mồ côi · BR-22 alt/opt/loop · BR-23 business context · BR-24 activity · BR-25 state — nhóm chốt thì Bảo gắn Trait vào 65 test DiagramEngine | Cả nhóm | |

---

## 7. Việc tiếp theo

1. Minh đã bàn giao M-01–M-07; các thành viên xác nhận/publish phần còn thiếu và đối chiếu **TASKS.md §1** trước ghép.
2. Xử lý contract Diagram → Render, gRPC/recovery Diagram và API job chung; không dùng unit PASS để suy ra liên service đã chạy đúng.
3. Thực hiện I-01–I-06 theo TASKS.md §7: CI, Compose/11 container, WF-01–03 rồi job admin/video. Chỉ tick sau khi có evidence tương ứng.
4. Mỗi người cập nhật phần tiến độ của mình. Không tự sửa trạng thái đồng đội hoặc Google Sheet từ kết quả kiểm tra local.

---

## 8. Nhật ký

### 14/11/2026 — Minh: sửa điều hướng khi kiểm thử app
- Thông báo InvitationSent mới mở `/invitations/mine`; Bell và UI-09 xử lý cả link project của thông báo cũ, không mở quyền project trước khi chấp nhận.
- UI-04/UI-07 giữ tab Sơ đồ, bộ lọc và trang danh sách qua xem/chỉnh sửa/lưu/hủy; link trực tiếp mặc định quay về tab Sơ đồ đúng project.
- Kiểm chứng trước ghép develop mới: 46 Notification tests, 18 client node tests, frontend build, 17 controlled-browser checks PASS; browser/API thật kiểm lời mời và 6 luồng quay lại danh sách PASS. Các fixture riêng đã xóa mềm; dữ liệu người dùng giữ nguyên.
- Sau ghép PR #30/#31: giữ fix runtime R16 của Thiện, cập nhật hai regression terminal đã lỗi thời để kiểm 409/không Outbox/giữ kết quả. Notification46 + Diagram69 + Workspace33 tests, client18, build và17 controlled-browser checks PASS. Không sửa runtime Diagram trong phần regression này.
- Theo xác nhận Minh, B-01–B-07 của Bảo đóng theo nghiệm thu leader. Các finding/acceptance tích hợp chung được theo dõi riêng, không suy dự án đã nghiệm thu 100%.

### 13/11/2026 — Thiện
- Xong:
  1. Tắt Fake Diagram Engine (`UseFake=false` ở cả appsettings.json, appsettings.Development.json và deploy/compose/diagram.yml); từ chối UML sai bằng Engine thật (HTTP 422 BR-UML).
  2. Bỏ project cố định, role switcher và dev-session fallback trong UI Diagram (`diagramApi.ts`, `routes.tsx`); tích hợp session chung, routing động theo dự án (`projects/:projectId/diagrams`, `projects/:projectId/reviews`), kiểm quyền bằng backend API thật.
  3. Hoàn thiện gRPC client Diagram (`GrpcDiagramEngineClient`): cấu hình deadline 5s, retry tối đa 2 lần chỉ với lỗi `Unavailable` (tổng 3 attempts), truyền `x-correlation-id`, ném HTTP 503 BR-503 khi hết lượt/timeout, giữ nguyên cancellation (`OperationCanceledException`).
  4. Khép migration drift Diagram sau khi chuyển sang shared Outbox/Inbox (`20261113050716_SyncSharedOutboxInbox.cs`): `dotnet ef migrations has-pending-model-changes` trả về sạch 0 drift.
  5. Sửa lỗi payload RenderRequested (DiagramType int 1..5, giải quyết INT-M01) và consumer recovery. Build Release & 65/65 unit tests PASS.
- Build & Test: `dotnet test src/Services/Diagram/Diagram.sln` 65/65 PASS; `npm --prefix client run check && npm --prefix client run build` PASS (0 errors); `dotnet publish` Release PASS.
- Bị chặn bởi: Không.
- Tiếp theo: Phối hợp I-01..I-06 tích hợp toàn hệ thống.

### 12/11/2026 — Minh
- Xong: triển khai và bàn giao M-01–M-07; PR #1/#2/#4/#5/#6/#8/#10 đã merge vào develop. M-07 commit `d6ad67a`, merge `761f0ed`.
- Kiểm chứng cuối M-07: 84 Render + 41 Notification unit PASS/0 skip, frontend build PASS, hai image build PASS và 13 nhóm container checks PASS; giới hạn Engine fixture đã nêu ở trên.
- Đồng bộ ghép nhóm: develop `81badf9`; năm service 271 unit PASS/0 skip và client build PASS. Payload Diagram → Render không tương thích đã tái hiện; root Compose chưa hợp lệ. Chưa có acceptance toàn hệ thống.
- Đang cập nhật: PROGRESS từ các PR/evidence đã xác minh; giữ nguyên dòng tiến độ và nhật ký của đồng đội.
- Tiếp theo: phối hợp I-04/I-06, kiểm contract và nghiệm thu trên stack thật khi dependencies sẵn sàng.

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
- Xong: B-02 (integration test Testcontainers, 11/11 xanh), B-03 (DiagramEngine: generator + luật UML từ v1, 53/53 test xanh), B-04 (4 RPC + interceptor + health + reflection, 65/65 test). Thiện/Minh có thể bỏ `UseFake` và trỏ `DiagramEngine:Url` = `http://localhost:5006`.
- Bug mới: không. Lệch tài liệu → mục 6 Q8.
- Bị chặn bởi: không.
- Xong thêm: B-05 (compose hạ tầng + engine + client chạy thật 5 container healthy; CI matrix).
- Xong thêm: B-06 (editor UI-06 + `useRenderStatus`, test bằng API giả).
- Bị chặn một phần: chạy editor với Diagram API thật chờ Thiện (Q10).
- Xong thêm: B-07 (seed-events, 22 sự kiện mẫu + test, reset-demo, README) → Bảo xong 7/7.
- Tiếp theo: chờ Q10 (Thiện) và Q11 (cả nhóm); ngày 4 chiều dẫn I-01/I-02 — thêm bước tạo dữ liệu CampusEats qua Gateway vào `reset-demo.ps1` khi API Identity/Workspace/Diagram đã có.

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
