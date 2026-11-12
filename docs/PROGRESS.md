# PROGRESS — UMLForge v2

> Cập nhật **cuối mỗi buổi làm** (hoặc khi merge PR). Ghi ngắn: xong gì, lỗi gì, làm gì tiếp.
> Khi nhờ AI làm tiếp, đưa file này cho AI đọc trước để nó biết dự án đang ở đâu.

**Cập nhật lần cuối:** 12/11/2026 — Minh (M-02 được nghiệm thu và giao Git delivery; chuẩn bị M-03)
**Giai đoạn hiện tại:** M-01 PR #1 đang mở vào develop. Minh đã nghiệm thu M-02 và giao commit/push/PR; chuẩn bị M-03 theo contract. B-01/B-02/B-04 và ghép toàn hệ thống còn chờ. Kế hoạch 4 ngày, xem TASKS.md §0.2

---

## 1. Tổng quan tiến độ

| Người | Ngày 1 | Ngày 2 | Ngày 3 | Ngày 4 sáng | Xong |
|---|---|---|---|---|---|
| Huy | ⬜ H-01 · ⬜ H-02 | ⬜ H-03 · ⬜ H-04 | ⬜ H-05 · ⬜ H-06 | ⬜ H-07 | 0/7 |
| Thiện | ⬜ T-01 · ⬜ T-02 | ⬜ T-03 · ⬜ T-04 | ⬜ T-05 · ⬜ T-06 | ⬜ T-07 | 0/7 |
| Minh | 🟨 M-01 · 🟨 M-02 | ⬜ M-03 · ⬜ M-04 | ⬜ M-05 · ⬜ M-06 | ⬜ M-07 | 0/7 |
| Bảo | ⬜ B-01 · ⬜ B-02 | ⬜ B-03 · ⬜ B-04 | ⬜ B-05 · ⬜ B-06 | ⬜ B-07 | 0/7 |
| Cả nhóm (ngày 4 chiều) | | | | ⬜ I-01 → I-06 | 0/6 |

Ký hiệu: ⬜ chưa làm · 🟨 đang làm · ✅ xong · ⛔ bị chặn

---

## 2. Đã xong

| Ngày | Task | Người | PR / ghi chú |
|---|---|---|---|
| 12/11/2026 | Soạn bản nháp PRD, ARCHITECTURE, DESIGN, RULES, TASKS, PROGRESS | Bảo | `docs/rebuild/` — chờ nhóm duyệt |
| 12/11/2026 | Tinh gọn phạm vi: bỏ Hangfire, realtime (Pub/Sub + SSE), email; giữ thông báo trong app | Bảo | Xem mục 2.1 |
| 12/11/2026 | M-01 final source review + Git delivery | Minh | Commit `590674c`, [PR #1](https://github.com/huy-do-it/diagramgen/pull/1) vào develop, OPEN/chưa merge. Fresh build0warning/error, unit43/43 và SQL/PlantUML10/10 PASS; chưa nhận Done toàn DoD. |

### 2.1 Quyết định đã chốt

| # | Ngày | Quyết định | Lý do |
|---|---|---|---|
| D-01 | 12/11/2026 | Kiến trúc **microservices**, mỗi service một database | Yêu cầu của nhóm khi xây lại |
| D-02 | 12/11/2026 | Background Job chỉ dùng **.NET BackgroundService** (như v1), **không Hangfire** | Đề ghi rõ BackgroundService; tái sử dụng code v1 |
| D-03 | 12/11/2026 | Message broker chỉ dùng **Redis Streams**, **không Pub/Sub**, không Redis List | Streams đủ tiêu chí 15%, không mất message, nhiều consumer |
| D-04 | 12/11/2026 | **Bỏ realtime**: client polling 2 s | Không phải tiêu chí chấm |
| D-05 | 12/11/2026 | **Bỏ email**, giữ **thông báo trong app** (Notification service) | Không phải tiêu chí chấm; vẫn có service thứ 5 tiêu thụ sự kiện |

---

## 3. Đang làm

| Task | Người | Bắt đầu | Ghi chú |
|---|---|---|---|
| Chốt tài liệu | Cả nhóm | — | Đọc TASKS.md mục 1 (hợp đồng chung) và trả lời câu hỏi mở ở mục 6 trước ngày 1 |
| M-01 review/integration | Minh | 12/11/2026 | PR #1 đang mở. Đối chiếu6docs PASS; sửa UTC Kind SQL readback. Adapter controlled gRPC7/7 và host4/4 PASS; B-01/B-04 team integration NOT RUN. Không có CI workflow trên baseline, không nhận local PASS là CI xanh. |
| M-02 delivery/integration | Minh | 12/11/2026 | Branch `feature/minh-m02-notification-api` độc lập từ develop `0e86974`; Minh nghiệm thu và giao Git delivery. Source khớp manifest29/29 trước thay checkpoint docs. Release0warning/error,19unit PASS/0skip và44real API/SQL assertions PASS. B-01/Identity/Gateway acceptance còn chờ. |
| M-03 preparation | Minh | 12/11/2026 | Chưa code. Cần source M-01 (PR #1 chưa merge) + B-01 Contracts.Events + B-02 Outbox/EventBus. Develop hiện chỉ có .gitkeep; tools/seed-events.ps1 chưa có. Chưa tạo bản thay thế các gói của Bảo. |

### Minh — checkpoint M-01/M-02

- Minh cho phép tạo develop từ main `0e86974`, mở PR M-01 vào develop theo RULES; không merge/main changes. M-02 không base trên PR M-01 chưa merge. M-01 có35file mới Render + checkpoint PROGRESS; M-02 chỉ Notification + checkpoint PROGRESS.
- M-02 áp dụng BR-33 ở service và owner filter trong SQL; read/read-all conditional update không làm mới ReadAt khi gọi lặp/đồng thời. Seed chỉ Development + Admin, tạo5thông báo cho chính Admin. Timestamps UTC; ProblemDetails có traceId/code/errors; CorrelationId giữ cả khi exception.
- B-01 chưa publish Web/Auth. M-02 bootstrap JWT Bearer/ProblemDetails/Swagger trực tiếp trong API theo contract TASKS, không tạo shared library thay Bảo hoặc phát token. JSON page đúng shape PagedResult. Khi B-01 có cần thay wiring sang shared defaults và kiểm tra lại; chưa nhận acceptance B-01/Identity/Gateway.
- Runtime test .NET8.0.32 riêng; SQL2022 container fixture không mount dữ liệu v1. JWT fixture và runners ngoài source, không secret trong appsettings. Evidence workspace `assignment-planning/v2-m01-evidence/` và `v2-m02-evidence/`; contract/chạy M-02 tại `src/Services/Notification/README.md`.
- M-03/M-04 còn cần thống nhất Inbox/ACK transaction, handler<5s vs pipeline30s, pending replay, interrupted existing job, ownership khi hai Worker và retry/event ordering. M-05 còn cần thống nhất actor/requestedBy và recipient ordering. Chưa tự đổi shared contract hoặc bắt đầu các task này.

---

## 4. Bug / vấn đề đã biết

| # | Mô tả | Mức độ | Service | Trạng thái | Ghi chú |
|---|---|---|---|---|---|
| — | Không còn lỗi đã tái hiện chưa xử lý trong M-01/M-02 local scope | | | | Shared dependency và integration vẫn đang chờ, không đồng nghĩa toàn hệ thống đã đạt. |

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

---

## 7. Việc tiếp theo

1. Gửi 6 file trong `docs/rebuild/` cho nhóm đọc; họp 30 phút chốt mục 6 và **TASKS.md mục 1 (hợp đồng chung)**.
2. Ngày 1, 8:00: Bảo làm B-01 (xong trước 11:00); 3 người còn lại bắt đầu H-01, T-01, M-01 ngay.
3. Cuối mỗi ngày: mỗi người tick ô của mình ở mục 1 và ghi nhật ký.
4. Cập nhật Google Sheet theo TASKS.md đã chốt.

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
