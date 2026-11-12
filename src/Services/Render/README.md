# Render — M-01 + M-03

Render sở hữu RenderDb; giữ Layered Architecture Worker → Service → Repository.
Nguồn contract: docs/TASKS.md §1/§4 và Contracts/BuildingBlocks của Bảo.
M-04 (reconcile/cleanup/report/admin jobs), Docker M-07 và tích hợp toàn nhóm chưa thực hiện ở đây.

## Luồng xử lý và transaction

1. Shared consumer đọc diagram.events, group render-svc, kiểm tra Inbox rồi gọi
   RenderRequestedHandler. Handler kiểm tra snapshot, bỏ qua business key đã tồn tại,
   thêm job Queued; **không gọi SaveChanges hoặc pipeline**.
2. Shared dispatcher commit job + Inbox trong một SaveChanges rồi ACK. Lỗi SQL khi
   intake được ném lên, không ACK; shared consumer retry pending/DLQ theo B-02.
3. RenderJobProcessor tạo DI scope mới mỗi vòng. RenderJobRunner claim job Queued:
   đổi Running, tăng Attempt và thêm Outbox RenderStarted trong cùng SaveChanges.
   Status + Attempt là concurrency tokens; hai Worker tranh cùng hàng chỉ một bên thắng.
4. Pipeline Validate → GeneratePlantUml qua gRPC → HTTP PlantUML chạy ngoài SQL transaction.
   Thành công: Done + artifact + Outbox RenderCompleted commit cùng nhau. Lỗi nghiệp vụ
   hoặc timeout: Failed + Error + Outbox RenderFailed, giữ artifact cũ nếu có.
5. Shared Outbox publisher chuyển sự kiện sang render.events. Redis down không làm mất
   kết quả đã commit; publisher retry. Delivery có thể trùng, consumer phải dùng Inbox.

**ACK xác nhận đã lưu yêu cầu bền vững, không xác nhận render hoàn tất.** Đây là điều chỉnh
M-03 đã được Minh duyệt so với pseudocode chạy pipeline ngay trong handler ở TASKS.md,
để tuân thủ PROGRESS N-04: dispatcher sở hữu SaveChanges cùng Inbox và handler phải nhanh.
Không thay shared EventBus/Outbox hoặc schema sự kiện của Bảo.

Unique (DiagramId, DiagramVersion) chặn job trùng cả khi intake đồng thời. EventId trùng
do Inbox xử lý; event mới cùng version không tạo job khác. Snapshot/RequestedBy lấy từ
payload; CorrelationId lưu từ envelope, khôi phục khi chạy pipeline và truyền tới gRPC,
log, Outbox. Migration nâng từ M-01 giữ job/artifact cũ; correlation rỗng của job cũ dùng
JobId làm fallback.

Lỗi hạ tầng sau claim hoặc shutdown giữ job **Running** đã commit, không tạo kết quả giả.
Job Queued tiếp tục sau restart; **phục hồi Running cần M-04**, hiện chưa tự chạy lại.
Lỗi commit cuối rollback trạng thái, artifact và completion event cùng nhau. Status/Attempt
cũng chặn writer cũ khi ownership đã đổi. Không nhận là exactly-once network execution.

## Pipeline và cấu hình

Pipeline trả RenderResult, không tự persist job hoặc phát event. Caller cancellation
được propagate; gRPC hạ tầng được propagate; model/deadline/renderer trả outcome an toàn.
Ngân sách pipeline mặc định/tối đa 30 giây (BR-27), RPC deadline 5 giây, tối đa 2 retry
chỉ khi Unavailable. JSON tối đa 256 KiB UTF-8, SVG tối đa 2,000,000 bytes; kiểm tra
HTTP/media type/XML/root, cấm DTD và active content cơ bản. UI vẫn phải sanitize SVG.

Worker dùng shared Web/Auth defaults, SQL health check /health, migration khi startup.
Không có public render/admin route mới. Appsettings không chứa secret. Cần đặt qua
environment hoặc secret store:

- ConnectionStrings__RenderDb, ConnectionStrings__Redis, Jwt__Key theo B-01.
- DiagramEngine__Url, PlantUml__BaseUrl trỏ tới các service đang chạy.
- DiagramEngine__UseFake=true chỉ khi chủ động test trong Development; Production từ chối fake.

Từ thư mục Render, dùng .NET 8 SDK theo global.json ở gốc repo:

~~~powershell
dotnet build Render.sln -c Release /warnaserror
dotnet test Render.sln -c Release --no-build
dotnet run --project Render.Worker
~~~

Máy Minh dùng SDK 8.0.428/runtime 8.0.34 riêng tại
C:/Users/phamv/AppData/Local/Microsoft/UMLForge/dotnet8-sdk, không đổi global.json/PATH.

## Verification M-03 — 12/11/2026

- Release /warnaserror: 0 warning/error; 54 unit PASS, 0 FAIL/SKIP (11 test mới).
- 24/24 checks PASS với SQL Server 2022 + Redis Streams 7 + HTTP PlantUML thật và
  hai production Worker processes: migration nâng M-01, intake rollback/pending replay,
  restart Queued sau ACK, duplicate/race, atomic claim/final writes, Redis outage/publisher
  recovery, stale writer, giữ SVG cũ, timeout, DLQ, shutdown và durable correlation.
- Generated gRPC client dùng Contracts thật của Bảo, gọi controlled HTTP/2 server;
  cả Validate và Generate nhận đúng CorrelationId. **Chưa kiểm chứng semantics B-04.**
- Shared seed-events chưa có trên baseline; harness ngoài repo phát canonical envelope
  vào Redis fixture, không bổ sung tool thay Bảo. Diagram producer/consumer, Gateway,
  browser, Compose toàn nhóm và CI: chưa nghiệm thu.

Evidence workspace ngoài repo: ../assignment-planning/v2-m03-evidence/ tính từ repo root;
unit tests nằm trong Render.Tests. SQL/Redis/PlantUML dùng container fixture riêng,
không mount dữ liệu v1. Các lần thử harness ban đầu và kết quả cuối được giữ riêng;
24/24 không phải acceptance WF toàn hệ thống. Evidence M-01 giữ ở v2-m01-evidence/.
