# Diagram/Review E2E với backend thật

Chạy stack Docker riêng gồm Identity, Workspace, Gateway, Diagram, DiagramEngine gRPC,
Render, Notification, nginx client, SQL Server, Redis Streams và PlantUML.

Yêu cầu: Docker Desktop đang chạy, Python 3, Node.js có global WebSocket (Node 22+),
Microsoft Edge. Đặt `EDGE_PATH` nếu Edge không ở vị trí mặc định trên Windows.
Không cần cài package Python/npm bổ sung cho test runner.

Chạy tại thư mục gốc repository:

```powershell
python -u tests/IntegrationTests/verify_diagram_review.py --output ../.antigravity/step3-develop-latest
```

Mặc định, mọi service được build từ cùng checkout hiện tại. Nếu các bản sửa chưa được
ghép vào cùng checkout, có thể chỉ định `--identity-source`, `--workspace-source`,
`--gateway-source` trỏ tới worktree tương ứng. Runner ghi revision từng source vào
`result.json` và label image.

Runner build tuần tự để tránh khóa metadata buildx trên Windows. `--skip-build` chỉ dùng
khi muốn chạy lại cùng source bằng các image `umlforge-e2e-*:local` đã build thành công;
không dùng tùy chọn này sau khi source thay đổi.

## Phạm vi

- Đăng nhập qua Gateway bằng user seed thật; tạo hai project có owner khác nhau.
- Mời/chấp nhận thành viên và gán giảng viên qua Workspace API; chờ sự kiện thật đồng bộ
  read model MemberAccess của Diagram.
- BR-11: user ngoài nhóm bị từ chối đọc, tạo, render, comment và xem Review của project khác.
- BR-29: chỉ Leader gửi duyệt; project còn diagram Draft bị từ chối, kể cả khi diagram
  thứ nhất đã Rendered. Snapshot chỉ chứa diagram thuộc project đang gửi.
- BR-30: Member/Leader và giảng viên chưa được gán không được quyết định Review;
  giảng viên được gán thực hiện request-changes/approve thành công.
- BR-32: giảng viên đọc/comment được nhưng không tạo/sửa/xóa/render/restore diagram.
- Render thực tế qua Outbox, Redis Streams, worker, DiagramEngine gRPC và PlantUML;
  xác nhận SVG/PlantUML và file ZIP thật.
- Headless Edge truy cập nginx client: ProjectDetail -> tạo diagram -> render -> SVG,
  Member không thấy gửi duyệt, Leader gửi duyệt, Lecturer comment/approve, project B không
  hiển thị diagram A. Browser không thay thế `fetch`, API response hoặc JWT.
- Xóa thành viên và đổi giảng viên qua API để xác nhận sự kiện thu hồi quyền.

Đây là bộ kiểm tra tích hợp của luồng Diagram/Review. Không thay thế toàn bộ BR tests,
kiểm tra tải, recovery/DLQ hoặc nghiệm thu cả năm loại diagram.

## Dữ liệu và artifact

Runner tạo network/container mang ID ngẫu nhiên, dùng database và mật khẩu chỉ dành cho
kiểm thử. Không dùng `.env` thật, không gọi dev MemberAccess endpoint, không ghi trực tiếp
SQL, không bắn sự kiện thủ công và không bật fake engine. Các thao tác nghiệp vụ qua
Gateway; browser đi qua client nginx.

Cuối lượt chạy, runner dọn container/network do chính lượt đó tạo, không xóa volume hoặc
container dự án đang có. Images được giữ để chạy lại. Artifact gồm log build, log từng
service, `browser.log` và `result.json`; password/JWT/refresh token được che trong log.
File session tạm dùng cho browser bị xóa sau test.

Trong working tree sạch, mã nguồn ứng dụng không bị runner chỉnh sửa. Các ảnh Docker
được build bằng phiên bản source tại thời điểm chạy; chỉ báo PASS khi toàn bộ assertions
và browser workflow kết thúc thành công.
