# Tự kiểm WF-01 và các mục nghiệm thu Huy ngày 14/11/2026

Đối chiếu: PRD §8 (WF-01), TASKS §7 (I-03), ARCHITECTURE §10 (schema/startup), DESIGN (trạng thái giao diện). Phần này kiểm Identity, Workspace, Gateway và đường nối Notification/Diagram/UI-11; không thay nghiệm thu WF-02/WF-03.

## 1. Chạy tự động bằng browser thật

Cần Docker Desktop đang chạy Linux containers, Python 3, Node.js có `WebSocket` tích hợp (Node 22 trở lên) và Edge hoặc Chrome. Không cần cài thêm npm package. Đứng tại repository:

```powershell
Set-Location D:\FPTU\Semester8\PRN232\asm\diagramgen
python -u tests/IntegrationTests/verify_workspace_flow.py --output ../.antigravity/wf01-self-test
$LASTEXITCODE
```

Nếu browser cài ở đường dẫn khác, đặt `UMLFORGE_BROWSER` thành đường dẫn executable trước khi chạy. Kết quả thành công là exit code `0`, `result.json` và `browser-result.json` cùng có `status: passed`.

Runner build 8 image từ source đang có, dùng context `src/` cho backend, `client/` cho frontend, tạo stack riêng 11 container với SQL/Redis/PlantUML và Engine thật. Nó lấy cấu hình từ `deploy/compose/*.yml`, dùng Gateway DNS service và nginx thật. Mật khẩu fixture sinh ngẫu nhiên; đăng nhập/đăng ký qua UI. Không inject JWT, membership hoặc chặn request để trả dữ liệu giả.

Sau khi chạy, runner xóa đúng container/network riêng của nó; không xóa volume hay dữ liệu stack chính. Dữ liệu workflow của fixture không tồn tại sau cleanup. Thư mục output giữ ảnh PNG, log build/container, danh sách request/status không chứa token, revision và SHA-256 source. Revision là HEAD lúc build; `sourceDirty` cho biết đang kiểm working tree có sửa chưa commit.

## 2. Tự thao tác trên hệ thống đang chạy

Mở địa chỉ Client của stack, mặc định `http://localhost:3000`. Dùng mật khẩu seed Development được cấu hình tại máy bạn; không ghi mật khẩu vào báo cáo/ảnh. Mở DevTools → Network, bật Preserve log, lọc `api/`. Không chia sẻ HAR chưa loại Authorization, refresh token và payload đăng nhập.

| Bước | Thao tác UI | Kết quả cần thấy |
|---|---|---|
| 1 | Student `student1` đăng nhập, tạo project với tên riêng cho lượt kiểm | Chuyển `/projects/{GUID}`; hàng `student1` có username, họ tên và vai trò Leader |
| 2 | Mời `student2` bằng tìm trong danh bạ và chọn kết quả | POST `/api/projects/{id}/invitations` trả 201; đóng modal, có toast |
| 3 | Đăng xuất; `student2` đăng nhập, mở chuông và bấm thông báo mời | Đi tới `/invitations/mine` trước khi là thành viên; không bị đưa thẳng vào project bị cấm |
| 4 | `student2` bấm Chấp nhận | POST accept thành công; lời mời rời danh sách Pending; project xuất hiện trong danh sách dự án |
| 5 | Leader mời `student3`; Student đăng nhập và chấp nhận | Project có 3 sinh viên — mốc WF-01 cơ bản |
| 6 | Tương tự với `student4` | Project có 4 sinh viên |
| 7 | Đăng ký thêm hai tài khoản Student qua UI, rồi Leader mời tài khoản thứ 5, chưa nhận lời | Project có 4 thành viên + 1 lời mời Pending |
| 8 | Leader thử mời tài khoản thứ 6 | HTTP 409, lỗi BR-07 hiển thị trong modal; không có lời mời mới |
| 9 | Tài khoản thứ 5 chấp nhận; Leader mở lại project | 5/5 sinh viên; nút Mời thành viên bị khóa. Runner kiểm thêm bằng API thật: mời người thứ 6 vẫn trả 409 |
| 10 | Leader gán `lecturer1` qua modal | Bảng có 6 hàng (5 sinh viên + 1 Lecturer), bộ đếm sinh viên vẫn 5/5; tên/họ tên đầy đủ |
| 11 | Member và Lecturer lần lượt mở project | Xem được project; không hiện nút sửa thông tin/mời của Leader |
| 12 | Tài khoản thứ 6 mở trực tiếp URL project | API 403, UI thông báo không có quyền; không lộ danh sách thành viên |
| 13 | Admin mở trang quản trị Render/job, kiểm thẻ Tài khoản và Dự án | GET danh sách job thành công; bấm Chạy ngay nhận POST 202 và UI “Đã chạy xong” |

Thông báo/danh bạ/membership Diagram được cập nhật qua Outbox → Redis Streams → Inbox, nên có độ trễ. Nếu chưa thấy, đợi rồi làm mới; không tự sửa SQL hoặc phát event giả để ép bước qua.

## 3. Kiểm startup Identity riêng

Code khởi tạo ở `src/Services/Identity/Identity.API/Program.cs`, trước `app.Run()`:

- Relational database: chạy `MigrateAsync()` ở cả Development và Production.
- Development mới seed demo; Production không seed tài khoản mẫu.
- Lỗi migration log Critical, throw và kết thúc process trước khi mở HTTP listener.

Phép kiểm có bằng chứng ngày 14/11 dùng SQL riêng: database mới → có schema/EF history → health 200; restart giữ dữ liệu; SQL login không có quyền tạo database → cả hai môi trường thoát nonzero và không nhận HTTP. Không thử lỗi quyền trên database đang dùng. Repo hiện có một migration Identity, nên evidence chỉ xác nhận apply migration đó và restart idempotent, chưa chứng minh một migration tương lai.

Bằng chứng local cùng workspace: `.antigravity/identity-startup-20261114/REPORT.md` (12 nhóm startup, 46 Identity tests); `.antigravity/r18-20261114/REPORT.md` (39 Workspace tests và HTTP/UI-11). Đây là thư mục ngoài repository `diagramgen`; cần gửi kèm khi bàn giao evidence.

## 4. Ghi kết quả và giới hạn

Lưu revision, trạng thái dirty, tên project/GUID, ảnh từng mốc và status Network. Khi lỗi, ghi actor, URL, thao tác, kỳ vọng/thực tế và file/line owning source; không chỉ ghi “UI lỗi”.

Registration hiện còn quan sát riêng: UI có lựa chọn Lecturer nhưng backend tự đăng ký luôn tạo Student. Workflow trên chỉ đăng ký Student; không dùng kết quả WF-01 để đóng vấn đề này. Render/review, recovery khi worker dừng, p95, CI và video vẫn cần nghiệm thu tương ứng.
