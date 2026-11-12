# Render — M-01

Phạm vi: RenderDb, migration, repository, pipeline và host tối thiểu .NET 8.
Contract chung: `docs/TASKS.md` §1 và §4. Không có consumer/Outbox (M-03),
reconcile/cleanup/admin API (M-04), Docker (M-07) trong M-01.

## Cấu trúc và transaction

- `Render.Worker`: composition root, migration khi khởi động, `/health` kiểm tra SQL thật.
- `Render.Service`: `IRenderPipeline.ProcessJobAsync(RenderJob, CancellationToken)`
  trả `RenderResult`; Validate → GeneratePlantUml qua Engine → HTTP PlantUML → SVG.
- `Render.Repositories`: RenderJobs, RenderArtifacts, RenderDailyReports và migration `InitRender`.
- `Render.Tests`: unit tests bằng HTTP/Engine được kiểm soát; không nhận là E2E.

Pipeline không mutate/persist job và không phát event. M-03 sẽ lưu trạng thái,
artifact và Outbox trong cùng transaction qua repository. Failure không trả artifact
mới; caller phải giữ artifact thành công cũ. Lỗi gRPC hạ tầng (`Unavailable`,
`Internal`...) được propagate để consumer quyết định retry; lỗi model/deadline/renderer
trả outcome thất bại an toàn. Cancellation của caller được propagate, không chuyển
shutdown thành Failed. Chưa có multi-worker claim/idempotency/recovery acceptance ở M-01.

`DiagramType` là mã int 1–5 của TASKS; không tạo enum trùng trong Contracts.
Job có unique `(DiagramId, DiagramVersion)`; Artifact FK chỉ tới job trong RenderDb.
Repository query hiện dành cho update nên tracking; chưa có API danh sách.

## B-01/B-04 dependency

Baseline `0e86974` chưa có project `UMLForge.Contracts.Grpc`. M-01 **không** tạo
proto/Contracts thay Bảo. Khi project ở đường dẫn chuẩn tồn tại, ProjectReference
và adapter `GrpcDiagramEngineClient` tự được compile. Adapter dùng `AddGrpcClient`,
deadline 5 giây/RPC, tối đa 2 retry chỉ khi Unavailable, truyền Activity TraceId
qua `x-correlation-id`.

Khi chưa có contract, build phần độc lập được nhưng real mode dừng startup bằng lỗi
rõ ràng. Không tự fallback sang fake. `DiagramEngine:UseFake=true` chỉ được dùng
trong Development và host luôn ghi cảnh báo. Fake trả một sequence Alice→Bob,
không kiểm chứng semantics của 5 loại diagram.

Ngân sách toàn pipeline mặc định/tối đa 30 giây (BR-27); HTTP client có thêm timeout
30 giây. Response body cũng chịu timeout/cancellation. JSON giới hạn 256 KiB UTF-8,
SVG tối đa 2,000,000 bytes; kiểm tra HTTP/error header/media type/XML/root, cấm DTD
và active content cơ bản. Frontend vẫn cần sanitizer theo RULES trước khi chèn SVG.

## Chạy độc lập

Từ thư mục này:

```powershell
dotnet build Render.sln -c Release /warnaserror
dotnet test Render.sln -c Release --no-build
# Cung cấp connection tới DB/fixture riêng bằng secret environment của máy.
# ConnectionStrings__RenderDb=<connection riêng, không commit/log>
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DiagramEngine__UseFake = 'true' # chỉ khi chủ động test bằng fake
dotnet run --project Render.Worker
```

Host mặc định qua launch profile ở `http://localhost:5004`, chỉ có `/health`.
Chưa có public render route hoặc Swagger/admin API để tránh tạo contract ngoài M-01.
Appsettings không chứa connection/secret; real mode mặc định `UseFake=false`.
Trước khi chạy thật, B-01 Web/Auth defaults và B-04 Engine cần được ghép/kiểm tra lại.

Máy Minh có ASP.NET 8.0.34 nhưng thiếu Core cùng patch. Lượt test bị aborted đã được
giữ evidence. Kiểm thử thành công dùng runtime riêng .NET 8.0.32 đã có, không đổi
runtime toàn máy. Local workaround:

```powershell
$env:DOTNET_ROOT_X64 = 'C:/Users/phamv/AppData/Local/Microsoft/UMLForge/dotnet8'
dotnet build Render.sln -c Release /warnaserror -p:RuntimeFrameworkVersion=8.0.32
dotnet test Render.sln -c Release --no-build
```

## Verification 12/11/2026

- Release /warnaserror: 0 warning/error; unit 43 PASS, 0 FAIL, 0 SKIP.
- SQL Server 2022 + HTTP PlantUML thật, Engine fake: 10 assertions PASS sau review
  (migration/rerun, render SVG, repository reload, version lookup, unique job,
  artifact FK, snapshot constraints, report date roundtrip/unique, UTC timestamp roundtrip).
- Adapter compile + HTTP/2 controlled fixture theo TASKS §1.7: 7 assertions PASS
  (retry, correlation, deadline, payload/mapping, invalid result, cancellation,
  actual timeout). Đây là test fixture ngoài repo, **không** phải B-01/B-04 thật.
- Host 4 assertions PASS: startup/migration + SQL health 200, SQL down health 503,
  từ chối fake ở Production, từ chối real mode khi thiếu B-01 contract.
- Team Engine/Streams/Gateway/browser: NOT RUN, thuộc các bước tích hợp sau.

Evidence ngoài repo: `../assignment-planning/v2-m01-evidence/` tính từ repository root.
SQL/PlantUML chạy trong container fixture riêng, từ image local `umlforge-local`
chỉ khởi động riêng SQL hoặc Jetty bằng entrypoint override; không mount dữ liệu v1.

Final source review đối chiếu đủ 6 docs: PASS trong phạm vi M-01. Sửa duy nhất sau
review: khôi phục UTC DateTime.Kind khi materialize SQL datetime2 theo RULES §6;
không đổi schema, migration/model diff không có pending changes. M-01 đủ điều kiện
giao review PR và bắt đầu M-02 độc lập. Shared defaults/Engine, CI và E2E toàn hệ
thống vẫn là acceptance tích hợp sau, không được suy ra PASS từ component tests.
