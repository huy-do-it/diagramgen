# PRD — UMLForge v2

> **Product Requirements Document**: đang xây gì, cho ai, giải quyết vấn đề gì, phạm vi tới đâu.
> Phiên bản: v2 (xây lại theo microservices) · Môn: PRN232 – Final Assignment · Nhóm: Huy, Thiện, Bảo, Minh

---

## 1. Tóm tắt sản phẩm

**UMLForge** là nền tảng web giúp **nhóm sinh viên làm đồ án capstone** tạo, kiểm tra, cộng tác và xuất **bộ 5 sơ đồ UML bắt buộc** (Business Context, Use Case, Sequence, Activity, State) cho báo cáo, và giúp **giảng viên hướng dẫn** góp ý và duyệt ngay trên hệ thống.

> *Một câu giá trị:* "Nhập như điền bảng, hệ thống tự kiểm tra luật UML và vẽ — cả nhóm cùng làm, giảng viên duyệt tại chỗ."

---

## 2. Bối cảnh và vấn đề

| # | Pain point | Nguyên nhân | Hậu quả (impact) | Minh chứng cần thu thập |
|---|---|---|---|---|
| P1 | Vẽ sơ đồ tốn thời gian | Kéo thả thủ công trên draw.io / Visio | ~30–60 phút / sơ đồ, × 5 loại × nhiều lần sửa | **Khảo sát ≥ 20 SV + đo thời gian thực tế** |
| P2 | Sơ đồ sai chuẩn UML | Không có công cụ kiểm tra (include/extend ngược, Activity thiếu end, State mồ côi…) | Bị trừ điểm báo cáo | Thống kê lỗi thường gặp từ góp ý giảng viên |
| P3 | Sơ đồ lệch nhau khi yêu cầu đổi | Mỗi sơ đồ là một file rời, không có phiên bản | Báo cáo mâu thuẫn | Phỏng vấn nhóm capstone |
| P4 | Cộng tác & góp ý rời rạc | File gửi qua Zalo/Drive, giảng viên góp ý qua tin nhắn | Chậm phản hồi, mất lịch sử | Phỏng vấn giảng viên |

> ⚠️ Số liệu P1–P4 hiện là giả định — **phải có khảo sát thật trước slide 03 (Problem)**.

---

## 3. Người dùng (Actors)

| Actor | Mô tả | Quyền chính |
|---|---|---|
| **Student** | Thành viên nhóm capstone | Tạo/sửa/render/xuất diagram trong project mình tham gia |
| **Team Leader** | Sinh viên tạo project (cũng là Student) | + mời/xóa thành viên, gán giảng viên, gửi duyệt |
| **Lecturer** | Giảng viên hướng dẫn (do Admin cấp quyền) | Xem project được gán, bình luận, duyệt / yêu cầu sửa (chỉ đọc diagram) |
| **Admin** | Quản trị hệ thống | Quản lý tài khoản, cấp quyền Lecturer, xem báo cáo vận hành |
| *PlantUML Server* | Hệ thống ngoài | Nhận mã PlantUML, trả SVG |
| *(tuỳ chọn)* *Gemini API* | Hệ thống ngoài | Sinh model JSON từ mô tả văn bản |

---

## 4. Mục tiêu và chỉ số đo được

| Mục tiêu | Chỉ số | Mục tiêu đặt ra |
|---|---|---|
| Giảm thời gian tạo sơ đồ | Thời gian tạo 1 Use Case diagram 10 use case | < 10 phút (so với ~40 phút) |
| Giảm lỗi UML | % sơ đồ nộp duyệt không còn lỗi gRPC | ≥ 95% |
| Render ổn định | Tỉ lệ render thành công 24h (báo cáo hằng ngày) | ≥ 98% |
| Phản hồi nhanh | Thời gian từ bấm Render tới có SVG | p95 < 5 giây |
| Đáp ứng đề PRN232 | 7 tiêu chí chấm | Đủ 7/7, có minh chứng |

---

## 5. Phạm vi

### 5.1 In-scope (MVP — sẽ demo)
- Tài khoản: đăng ký, đăng nhập JWT + refresh token, 4 vai trò.
- Project & nhóm: tạo project, mời / chấp nhận / xóa thành viên, gán giảng viên.
- Diagram 5 loại: **form nhập liệu** + chế độ JSON, khung điền chỗ trống, ví dụ mẫu, phiên bản.
- Kiểm tra luật UML qua **gRPC** (Validate, Suggest, ValidateProject streaming).
- Render **bất đồng bộ theo sự kiện** (Redis Streams); client hỏi lại trạng thái mỗi 2 giây (polling).
- Xuất SVG / PNG / ZIP cả project.
- Duyệt: gửi duyệt, bình luận, duyệt / yêu cầu sửa.
- Thông báo **trong app** (chuông 🔔, danh sách, đánh dấu đã đọc).
- Job nền bằng **BackgroundService** (giống v1): báo cáo hằng ngày, dọn dữ liệu, reconcile, hết hạn lời mời, dọn token/thông báo cũ.

### 5.2 Nice-to-have (làm nếu còn thời gian)
- AI `TextToModel` (Gemini) sinh JSON từ văn bản.
- So sánh khác biệt giữa 2 phiên bản diagram.
- Kiểm tra chéo cả project (actor ở Use Case phải xuất hiện ở Sequence…).

### 5.3 Out-of-scope (có lý do)
| Hạng mục | Lý do |
|---|---|
| Editor kéo thả | Trái với định hướng "Diagram as Code / form" |
| Nhiều người sửa realtime cùng lúc | Phức tạp (CRDT), không phải tiêu chí chấm |
| OAuth Google/Facebook | JWT tự cấp đủ yêu cầu đề |
| Kubernetes / cloud production | Docker Compose đủ tiêu chí deploy |
| Kafka | Đề cho phép Redis; Kafka quá nặng cho quy mô đồ án |
| Hangfire | Đề yêu cầu .NET BackgroundService; nhóm đã có sẵn code BackgroundService từ v1 |
| Realtime (SSE / SignalR / Redis Pub/Sub) | Không phải tiêu chí chấm; polling 2 giây đủ dùng |
| Gửi email (SMTP / MailHog) | Không phải tiêu chí chấm; thông báo trong app thay thế |

---

## 6. Nghiệp vụ và tính năng

| Mã | Nghiệp vụ | Service | Tính năng chính |
|---|---|---|---|
| NV-01 | Tài khoản & xác thực | Identity | Đăng ký, đăng nhập, refresh, hồ sơ, Admin quản lý user |
| NV-02 | Dự án & nhóm | Workspace | CRUD project, mời/chấp nhận/xóa thành viên, gán giảng viên |
| NV-03 | Mô hình hóa sơ đồ | Diagram | CRUD diagram, form/JSON, validate, phiên bản, render, xuất |
| NV-04 | Kiểm tra & sinh mã UML | DiagramEngine (gRPC) | Validate, Suggest, GeneratePlantUml, ValidateProject |
| NV-05 | Render bất đồng bộ | Render | Consumer render, trạng thái job, artifact |
| NV-06 | Duyệt & góp ý | Diagram (Review) | Gửi duyệt, bình luận, duyệt / yêu cầu sửa |
| NV-07 | Thông báo | Notification | Thông báo trong app: tạo từ sự kiện, danh sách, số chưa đọc, đánh dấu đã đọc |
| NV-08 | Báo cáo & vận hành | Render, Identity | Báo cáo hằng ngày, dọn dữ liệu, reconcile, dashboard Admin |

### 6.1 User stories (rút gọn)
- **US-01** Là Student, tôi muốn đăng ký và đăng nhập để có workspace riêng.
- **US-02** Là Leader, tôi muốn tạo project và mời bạn cùng nhóm để cùng làm sơ đồ.
- **US-03** Là Student, tôi muốn nhập sơ đồ bằng form và được báo lỗi UML ngay khi lưu.
- **US-04** Là Student, tôi muốn bấm Render rồi làm việc khác, thấy trạng thái tự cập nhật và nhận thông báo khi xong.
- **US-05** Là Leader, tôi muốn gửi cả bộ sơ đồ cho giảng viên duyệt.
- **US-06** Là Lecturer, tôi muốn bình luận trên từng sơ đồ và duyệt / yêu cầu sửa.
- **US-07** Là Student, tôi muốn xuất cả bộ sơ đồ thành ZIP để chèn vào báo cáo.
- **US-08** Là Admin, tôi muốn xem báo cáo render hằng ngày và cấp quyền Lecturer.

---

## 7. Business Rules

| Mã | Quy tắc | Service | Phản hồi khi vi phạm |
|---|---|---|---|
| BR-01 | Username là duy nhất | Identity | 409 |
| BR-02 | Mật khẩu ≥ 8 ký tự | Identity | 400 |
| BR-03 | Tài khoản bị khóa không đăng nhập được | Identity | 403 |
| BR-04 | Chỉ Admin cấp/thu hồi quyền Lecturer | Identity | 403 |
| BR-05 | Refresh token chỉ dùng một lần (rotation) | Identity | 401 |
| BR-06 | Người tạo project là Leader | Workspace | — |
| BR-07 | Nhóm tối đa 5 thành viên | Workspace | 409 |
| BR-08 | Chỉ Leader mời / xóa thành viên | Workspace | 403 |
| BR-09 | Lời mời hết hạn sau 7 ngày | Workspace (job) | 410 |
| BR-10 | Chỉ gán được user có vai trò Lecturer | Workspace | 400 |
| BR-11 | Chỉ thành viên project (và giảng viên được gán) truy cập diagram | Diagram | 403 |
| BR-12 | ModelJson phải khớp loại diagram | Diagram | 400 |
| BR-13 | Không sửa diagram khi đang Pending/Rendering | Diagram | 409 |
| BR-14 | Không render trùng khi đang Pending/Rendering | Diagram | 409 |
| BR-15 | Mỗi lần lưu tạo một phiên bản mới | Diagram | — |
| BR-16 | Hai người cùng sửa → người lưu sau bị từ chối (RowVersion) | Diagram | 409 |
| BR-17 | Diagram Rendered/Failed bị sửa → quay về Draft | Diagram | — |
| BR-18…BR-25 | Luật UML (include/extend, generalization, vòng lặp, use case mồ côi, alt/opt/loop, context, state…) | DiagramEngine | Trả lỗi / cảnh báo |
| BR-26 | Mỗi (DiagramId, Version) chỉ render một lần | Render | Bỏ qua message |
| BR-27 | Render tối đa 30 giây | Render | Failed (timeout) |
| BR-28 | Render lỗi giữ lại SVG cũ | Render / Diagram | — |
| BR-29 | Chỉ gửi duyệt khi mọi diagram đã Rendered | Diagram | 400 |
| BR-30 | Chỉ giảng viên được gán mới duyệt được | Diagram | 403 |
| BR-31 | Sửa diagram sau khi đã duyệt → phải gửi duyệt lại | Diagram | — |
| BR-32 | Lecturer chỉ xem và bình luận, không sửa | Diagram | 403 |
| BR-33 | User chỉ thấy thông báo của mình | Notification | 403 |
| BR-34 | Thông báo chỉ gửi cho người liên quan (thành viên project / giảng viên được gán), không gửi cho chính người thực hiện | Notification | — |
| BR-35 | Thông báo đã đọc quá 30 ngày bị xóa tự động | Notification (job) | — |

---

## 8. Workflow lõi (sẽ demo)

| WF | Câu chuyện | Actor | Kết quả | Ngoại lệ demo |
|---|---|---|---|---|
| WF-01 | Leader tạo project "CampusEats", mời 2 bạn; bạn thấy lời mời ở chuông thông báo và chấp nhận | Leader, Student | Project có 3 thành viên | Mời người thứ 6 → 409 (BR-07) |
| WF-02 | Thành viên nhập Use Case bằng form → Validate → Render, trạng thái tự chuyển Pending → Rendered | Student | Diagram Rendered + thông báo | Include từ actor → lỗi gRPC; tắt Render giữa chừng → tự phục hồi |
| WF-03 | Leader gửi duyệt → Lecturer bình luận, duyệt → nhóm xuất ZIP | Leader, Lecturer | Review Approved, file ZIP | Lecturer không được gán duyệt → 403 (BR-30) |

---

## 9. Yêu cầu phi chức năng

| Nhóm | Yêu cầu |
|---|---|
| Hiệu năng | API p95 < 300 ms (không tính render); render p95 < 5 s |
| Tin cậy | Không mất lệnh render khi service sập (Streams + XCLAIM); không xử lý trùng (Inbox) |
| Bảo mật | JWT 15 phút + refresh 7 ngày; BCrypt; không log mật khẩu/token; gRPC chỉ trong mạng nội bộ |
| Khả năng mở rộng | Chạy được nhiều bản Render song song (consumer group) |
| Quan sát | Log có cấu trúc kèm CorrelationId xuyên suốt; `/health` cho mọi service |
| Triển khai | Một lệnh `docker compose up` chạy toàn bộ |
| Kiểm thử | Mỗi BR có ít nhất 1 test; CI chạy build + test cho mọi PR |

---

## 10. Đối chiếu tiêu chí chấm PRN232

| Tiêu chí | Trọng số | Đáp ứng bằng |
|---|:-:|---|
| Kiến trúc & thiết kế | 20% | Microservices, database-per-service, API Gateway, C4 |
| REST API | 20% | 5 service REST, JWT 4 vai trò, ProblemDetails, search/filter/sort/paging |
| Background Job | 10% | Chỉ .NET BackgroundService: xử lý bất đồng bộ (consumer, outbox) + định kỳ bằng `PeriodicTimer` (báo cáo, dọn dữ liệu, reconcile…) |
| Message Broker | 15% | Redis Streams (consumer group), Outbox/Inbox/DLQ — nhiều producer, nhiều consumer |
| gRPC | 15% | DiagramEngine: 3 RPC unary + 1 server streaming, 2 service gọi |
| Docker | 10% | 11 container, healthcheck, 1 lệnh chạy |
| Tài liệu & thuyết trình | 10% | Khung slide V11, Swagger/OpenAPI, README, traceability |

---

## 11. Câu hỏi còn mở

- [ ] Hạn nộp / ngày bảo vệ chính xác?
- [ ] Cả nhóm đã đồng ý xây lại chưa? Phân công theo service?
- [ ] Có giữ actor Lecturer và luồng duyệt (NV-06) không?
- [ ] AI `TextToModel` có trong phạm vi không?
- [ ] Chỉ Docker Compose hay cần deploy cloud?
- [ ] Repo mới hay làm tiếp trên repo cũ?
