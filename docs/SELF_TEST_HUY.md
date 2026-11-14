# Self-test UMLForge v2 — bắt đầu từ phần của Huy

**Người dùng tài liệu:** Huy và thành viên kiểm tra chéo phần Identity, Workspace, Gateway, client.
**Baseline đối chiếu:** `eee3dd8`, ngày 13/11/2026. Khi test bản khác, ghi SHA thực tế vào biên bản cuối file.
**Thứ tự:** chuẩn bị → H-01 → H-02 → H-03 → H-04 → H-05 → H-06 → H-07 → tích hợp phần đồng đội.

Đây là hướng dẫn để tự thực hiện và ghi kết quả. Các ô chưa đánh dấu không có nghĩa là đã PASS.
Mã `H01-01`, `H03-01`… dưới đây là mã **test case**, không phải Business Rule mới.

## 1. Đối chiếu với sáu tài liệu

| Nguồn | Dùng để kiểm tra |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Service/port, database riêng, Gateway, Outbox/Inbox, Redis Streams, gRPC, Docker, jobs |
| [DESIGN.md](DESIGN.md) | UI-01/02/03/04/05/10, loading/empty/error/success, modal, toast, accessibility; điểm nối UI-07/08/11 |
| [PRD.md](PRD.md) | NV-01, NV-02; BR-01…10; luồng liên quan BR-11/29/30/32 và WF-01…03 |
| [PROGRESS.md](PROGRESS.md) | Biết phần nào đã bàn giao và phần nào còn chờ nghiệm thu; không dùng dấu ✅ thay bằng chứng test |
| [RULES.md](RULES.md) | REST/ProblemDetails, ETag/If-Match, bảo mật, logging, database-per-service, scope và quy tắc không phá dữ liệu |
| [TASKS.md](TASKS.md) | Contract chi tiết §1, H-01…H-07, API/UI của từng người và I-01…I-06 |

ARCHITECTURE chỉ định TASKS §1 là nguồn chính thức cho contract. Ví dụ route job đang dùng là
`/api/admin/<service>/jobs/...`; ARCHITECTURE/RULES còn ví dụ cũ `/api/admin/jobs/...`, đã được PROGRESS ghi ở Q8.
Không tự đổi route hoặc BR để làm một case đang lỗi thành PASS.

| Phần của Huy | Nội dung cần chứng minh | Đối chiếu chính |
|---|---|---|
| H-01 | Đăng ký, đăng nhập, JWT, refresh rotation, logout | NV-01; BR-01/02/03/05; UI-01/02 |
| H-02 | Admin users, phân quyền, sự kiện Identity, purge job | BR-03/04; UI-10; Jobs contract |
| H-03 | Project, Leader, thành viên, ETag, xóa mềm | NV-02; BR-06/08; UI-03/04 |
| H-04 | Invitation, giới hạn nhóm, Lecturer, danh bạ, expiration job | BR-07/08/09/10; UI-05; WF-01 |
| H-05 | Routing, JWT, rate limit, CORS, CorrelationId | TASKS H-05 và §1.1/1.2 |
| H-06 | UI và kết nối đúng project/phiên người dùng | DESIGN §6…10; TASKS H-06 |
| H-07 | Build, BR tests, Docker, migration, Swagger | TASKS H-07; RULES Definition of Done |

## 2. Chuẩn bị môi trường và dữ liệu

### 2.1 Chọn cách test

- **Test tay để hiểu/demo:** khởi động Compose ở mục 2.2; dùng UI và Swagger theo mục 3…9.
- **Chạy hồi quy nhanh:** dùng các lệnh tại mục 10.
- **E2E độc lập, không đụng dữ liệu Compose đang có:** dùng runner tại mục 11. Runner tự tạo/dọn stack riêng; không giữ môi trường đó để bấm UI sau khi chạy xong.

Các lệnh PowerShell bên dưới chạy từ thư mục repository:

```powershell
Set-Location D:\FPTU\Semester8\PRN232\asm\diagramgen
git status --short
git branch --show-current
git rev-parse HEAD
docker version
docker compose version
```

Nếu cần cập nhật develop, kiểm tra working tree trước. Khi có thay đổi chưa commit, không checkout/pull đè lên chúng.
Với working tree sạch, dùng `git pull --no-rebase --no-commit origin develop`; nếu diverged, merge có thể chờ commit.

### 2.2 Chạy hệ thống để test tay

Chỉ copy `.env` khi file chưa có; không ghi đè cấu hình đang dùng:

```powershell
if (!(Test-Path deploy/.env)) { Copy-Item deploy/.env.example deploy/.env }
$env:GIT_COMMIT = git rev-parse HEAD
docker compose --env-file deploy/.env -f deploy/docker-compose.yml config --quiet
docker compose --env-file deploy/.env -f deploy/docker-compose.yml up --build -d
docker compose --env-file deploy/.env -f deploy/docker-compose.yml ps
```

Kiểm tra/sửa giá trị dev trong `deploy/.env` bằng editor trước khi chạy. Không dán nội dung file này vào log/biên bản.
`config --quiet` kiểm tra Compose mà không in các giá trị cấu hình đã nội suy.

**Mong đợi:** 11 container khởi động, backend/infrastructure có healthcheck đạt healthy; client mở được.
Nếu một container chưa healthy, xem log service đó trước khi test nghiệp vụ:

```powershell
docker compose --env-file deploy/.env -f deploy/docker-compose.yml logs --tail 80 identity workspace gateway
```

Không dùng `down -v`, `DROP DATABASE` hoặc reset volume để chữa lỗi khởi động.

| Địa chỉ | Mục đích |
|---|---|
| `http://localhost:3000` | Client nginx — ưu tiên dùng khi nghiệm thu |
| `http://localhost:5000/swagger` | Gateway portal, link Swagger các service |
| `http://localhost:5001/swagger` | Identity Swagger |
| `http://localhost:5002/swagger` | Workspace Swagger |
| `http://localhost:5003/swagger` | Diagram Swagger |
| `http://localhost:5004/swagger` | Render Swagger |
| `http://localhost:5005/swagger` | Notification Swagger |
| `http://localhost:5000/health` | Gateway health |
| `http://localhost:5001/health`…`:5005/health` | Health từng service |
| `127.0.0.1:5006` | gRPC debug; không kiểm tra bằng HTTP GET thông thường |
| `http://localhost:8085` | PlantUML |

Swagger từng service dùng để kiểm tra độc lập. Với các case ghi “qua Gateway”, gọi `http://localhost:5000/api/...`
hoặc thao tác trên client nginx. Swagger Identity thành công chưa chứng minh Gateway hoạt động.

### 2.3 Chuẩn bị actor và project

Identity seed Development gồm `admin`, `student1`…`student4`, `lecturer1`, `lecturer2`.
Mật khẩu seed xem `Seed:DefaultPassword` trong cấu hình Identity đang dùng; có thể bị override bằng environment.
Seeder chỉ thêm dữ liệu khi database chưa có user; sửa cấu hình không tự đổi mật khẩu user đã tồn tại.

| Actor | Tài khoản đề xuất | Quyền trong dữ liệu test |
|---|---|---|
| Admin | `admin` | Quản lý user, chạy jobs; không dùng thay Leader trong các bài phân quyền |
| Leader A | `student1` | Tạo project A |
| Member A | `student2` | Nhận lời mời vào A |
| Người ngoài A / Leader B | `student3` | Tạo B, không tham gia A |
| Lecturer A | `lecturer1` | Được gán cho A |
| Lecturer B | `lecturer2` | Được gán cho B, không được gán cho A |
| User tạm 1, 2 | Tự đăng ký, username riêng cho mỗi lượt | Dùng test khóa/đổi role/giới hạn nhóm |

**Student/Lecturer/Admin là role JWT. Leader là membership theo project.**
Không đổi role toàn hệ thống thành `Leader`; cùng một Student có thể là Leader A và Member của project khác.

Ghi các biến sau trong ghi chú cá nhân hoặc Postman environment, không commit token/mật khẩu:

```text
BASE_URL=http://localhost:5000
RUN_ID=<mốc thời gian của lượt test>
ADMIN_ID=<Guid>
PROJECT_A_ID=<Guid>
PROJECT_B_ID=<Guid>
MEMBER_A_ID=<Guid>
LECTURER_A_ID=<Guid>
LECTURER_B_ID=<Guid>
INVITATION_ID=<Guid>
DIAGRAM_A_ID=<Guid>
REVIEW_A_ID=<Guid>
```

Các chuỗi `<...>` trong tài liệu phải thay bằng dữ liệu thực tế trả về, không phải GUID demo.
Tên project nên có RUN_ID, ví dụ `SelfTest-A-<RUN_ID>`, để không lẫn với lượt cũ.
Mỗi actor dùng profile trình duyệt riêng hoặc đăng xuất/đăng nhập rõ ràng. Tab mới cùng profile có thể sao chép
sessionStorage ban đầu từ tab mở nó; luôn xác nhận tên user đang hiển thị.

### 2.4 Quy ước gửi API

Mọi route trong bảng dùng BASE_URL ở trên. Request cần đăng nhập gửi:

```http
Authorization: Bearer <ACCESS_TOKEN_CUA_DUNG_ACTOR>
Content-Type: application/json
```

Lấy token từ response login trong Swagger/công cụ HTTP; không đưa token vào ảnh nộp hoặc biên bản.
Trong Swagger, xem mô tả ô Authorize để nhập đúng raw token hoặc `Bearer ...` của service tương ứng.
Các response lỗi cần xem cả HTTP status và ProblemDetails (`code`, `detail`, `errors` nếu có).

## 3. H-01 — Identity: auth, JWT, refresh

### 3.1 Đăng ký và đăng nhập

Body đăng ký user tạm:

```json
{
  "username": "<TEST_USER_1>",
  "password": "<TEST_PASSWORD_AT_LEAST_8_CHARACTERS>",
  "fullName": "Self Test User 1"
}
```

Body login: `{ "username": "<USERNAME>", "password": "<TEST_PASSWORD>" }`.

| Case | Thao tác | Mong đợi |
|---|---|---|
| H01-01 | `POST /api/auth/register` với user mới | 201; id là Guid; role Student; không trả password/hash/token từ register |
| H01-02 | Đăng ký lại cùng username | 409, BR-01; không tạo thêm user |
| H01-03 | Password dưới 8 ký tự | 400, BR-02; có lỗi trường Password |
| H01-04 | Username/fullName rỗng hoặc vượt giới hạn theo Swagger/validator | 400, validation error; không tạo user |
| H01-05 | Login đúng | 200; có accessToken, refreshToken, expiresIn, user; user.id đúng người đăng nhập |
| H01-06 | Login sai mật khẩu | 401; không cấp token |
| H01-07 | `GET /api/users/me` bằng token vừa cấp | 200; đúng id/username/role |
| H01-08 | `/api/users/me` thiếu token hoặc token sai | 401 |

JWT claim theo TASKS §1.2: `sub` là UserId, `unique_name`, `role`, `iss`, `aud`, `exp`.
Kiểm tra bằng công cụ local; không upload token đang dùng lên website giải mã token.
Thời hạn access/refresh phải khớp cấu hình môi trường, không giả định mọi môi trường đều có cùng hạn dev.

### 3.2 Refresh rotation và logout API

Test bằng phiên login riêng, không dùng phiên đó đồng thời trong UI vì UI có thể tự rotate token.

1. Login mới, lưu access token A0 và refresh token R0 trong bộ nhớ công cụ HTTP.
2. `POST /api/auth/refresh` body `{ "refreshToken": "<R0>" }` → 200; nhận A1/R1 mới.
3. Gọi `/api/users/me` với A1 → 200.
4. Dùng lại R0 → 401/BR-05: đây là hành vi phát hiện token reuse.
5. Thử R1 sau bước reuse → 401 vì phiên liên quan đã bị thu hồi. Login mới để tiếp tục các bài khác.
6. Với phiên login mới khác, gọi `POST /api/auth/logout` với refresh token hiện tại → 204.
7. Refresh token vừa logout không được dùng lại → 401.

Access token JWT đã cấp có thể còn hợp lệ đến `exp`; không kết luận logout API sai chỉ vì JWT cũ chưa hết hạn
vẫn được middleware chấp nhận. Tách việc kiểm tra refresh-token revocation khỏi việc UI xóa phiên local.

## 4. H-02 — Admin users, sự kiện và job Identity

### 4.1 Quản lý user

| Case | Actor/thao tác | Mong đợi |
|---|---|---|
| H02-01 | Admin: `GET /api/users?page=1&pageSize=2&sort=createdAt:desc` | 200; PagedResult có items/page/pageSize/totalItems/totalPages; tối đa 2 items |
| H02-02 | Thêm `search=<TEST_USER_1>`, `role=Student`, `isLocked=false` | Dữ liệu đúng từng bộ lọc; so sánh trước/sau, không chỉ thấy 200 |
| H02-03 | Student gọi GET users hoặc PATCH quản trị | 403; không thay dữ liệu |
| H02-04 | Admin: `PATCH /api/users/<TEST_USER_ID>/role`, body `{ "role": "Lecturer" }` | 200; login mới của user có role Lecturer; Workspace directory đồng bộ sau sự kiện |
| H02-05 | Role không hợp lệ, ví dụ `Leader` | 400; không coi Leader là role JWT |
| H02-06 | Admin: PATCH `.../lock`, body `{ "isLocked": true }` cho user tạm | 200; login mới của user đó nhận 403/BR-03; refresh token bị thu hồi |
| H02-07 | Admin tự khóa đúng ADMIN_ID của mình | 403/BR-04; Admin vẫn sử dụng được |
| H02-08 | Mở khóa user tạm bằng `{ "isLocked": false }` | Login lại thành công |

Sau bài này, trả role user tạm về Student và mở khóa để không làm sai dữ liệu của bài sau.
Sau đổi role, login lại để nhận claim mới; không dùng JWT cũ để kết luận role mới không hoạt động.

### 4.2 Chứng minh sự kiện Identity → Workspace

1. Đăng ký một username mới với RUN_ID.
2. Bằng tài khoản đã đăng nhập, gọi `GET /api/directory/users?search=<USERNAME>`.
3. Mong đợi user xuất hiện với id/role đúng sau khi consumer đồng bộ; không cần tạo bản sao bằng tay.
4. Đổi role user đó bằng Admin, gọi lại directory với bộ lọc tương ứng.

Đọc log/stream nếu chưa đồng bộ:

```powershell
docker compose --env-file deploy/.env -f deploy/docker-compose.yml logs --tail 80 identity workspace
docker exec umlforge-redis redis-cli XREVRANGE identity.events + - COUNT 3
docker exec umlforge-redis redis-cli XINFO GROUPS identity.events
```

Các lệnh Redis ở đây chỉ đọc. Không dùng `XADD` hay dev token/member-access endpoint để làm bài E2E “xanh”.
Sự kiện đến chậm không phải lý do bỏ kiểm tra quyền; nếu API đã thành công nhưng read model không hội tụ,
ghi lỗi và kèm log/CorrelationId.

### 4.3 Shared job Identity và UI-11

| Request | Actor | Mong đợi |
|---|---|---|
| `GET /api/admin/identity/jobs` | Admin | 200; array có jobName/interval/lastResult; có `purge-refresh-tokens` |
| `POST /api/admin/identity/jobs/purge-refresh-tokens/run` | Admin | 202; body JobRunResult có jobName/succeeded/summary/durationMs/finishedAt/trigger |
| POST như trên | Student / không token | 403 / 401 |
| `POST /api/admin/identity/jobs/not-existing/run` | Admin | 404, code JOB_NOT_FOUND trong ProblemDetails |

Phải kiểm tra `succeeded == true`, `trigger == "manual"` và log. HTTP 202 một mình chưa chứng minh job chạy thành công.
Purge được 0 token là hợp lệ khi không có token đủ cũ. Dùng PurgeRefreshTokensJobTests để kiểm logic xóa token
đủ điều kiện; không sửa ngày/token trong database dùng chung chỉ để tạo ảnh demo.

Trong UI: Admin mở `/admin/render` → khu Job định kỳ → Tài khoản → Chạy ngay.
Mong đợi thấy job, nút có trạng thái đang chạy, kết quả/lastResult cập nhật; Student không vào được console Admin.

## 5. H-03 — Project, Leader, thành viên, concurrency

### 5.1 Tạo A/B và kiểm tra cách ly

1. Student1 gọi `POST /api/projects`, body `{ "name": "SelfTest-A-<RUN_ID>", "description": "Manual self test" }`.
2. Mong đợi 201; lưu PROJECT_A_ID thật từ response; ownerId là Student1, membership của Student1 là Leader (BR-06).
3. Student3 tạo project B tương tự; lưu PROJECT_B_ID.
4. Gọi `GET /api/projects` bằng từng actor; A thuộc danh sách Student1, B thuộc Student3; không lẫn dữ liệu người ngoài.

| Case | Thao tác | Mong đợi |
|---|---|---|
| H03-01 | GET `/api/projects?page=1&pageSize=2&sort=name:asc&search=SelfTest` | 200/PagedResult; tìm/lọc/sắp xếp/phân trang đúng dữ liệu |
| H03-02 | Student1 GET `/api/projects/<A>` và `.../<A>/members` | 200; đúng id và membership; detail có header ETag |
| H03-03 | Student3 GET detail/members A | 403; không được xem dữ liệu A |
| H03-04 | Sau khi Student2 vào A, Student2 PUT/DELETE A hoặc xóa thành viên A | 403/BR-08 |
| H03-05 | Leader xóa chính membership Leader | 403/BR-08 |

H03-04 chạy sau khi hoàn tất mời/accept ở mục 6.1; ghi chờ kiểm tra rồi quay lại case này.

### 5.2 ETag/If-Match

1. Leader GET detail A hai lần, giữ cùng ETag E0 cho hai request mô phỏng hai người đang sửa.
2. PUT `/api/projects/<A>`, body `{ "name": "SelfTest-A-updated-<RUN_ID>", "description": "Updated" }`, header `If-Match: <E0>` → 200.
3. PUT lần thứ hai với E0 cũ → 409/CONCURRENCY_CONFLICT; không ghi đè bản vừa lưu.
4. PUT thiếu If-Match → 409 theo implementation Workspace hiện tại.
5. GET lại lấy ETag mới rồi cập nhật → 200.

Giữ dấu nháy nếu header trả ETag có nháy. Đây là bài concurrency theo RULES §4; không tạo thêm mã BR riêng.
Chỉ test xóa mềm bằng project phụ C: Leader DELETE C → 204; GET/list sau đó không còn trả C.
Không xóa A/B trước khi hoàn tất các bài tích hợp.

## 6. H-04 — Invitation, giới hạn nhóm, Lecturer, expiration

### 6.1 Mời và chấp nhận thành viên A

1. Xác nhận Student2 xuất hiện trong `/api/directory/users?search=student2`.
2. Leader A: `POST /api/projects/<A>/invitations`, body `{ "username": "student2" }` → 201/Pending; lưu INVITATION_ID.
3. Student2: `GET /api/invitations/mine` → có đúng lời mời tới A.
4. Student3 thử `POST /api/invitations/<ID>/accept` → 403; không chiếm được lời mời.
5. Student2 accept đúng ID → 200/Accepted; membership A có Student2 với role Member.
6. Chờ quyền Diagram hội tụ: Student2 GET `/api/diagrams?projectId=<A>` → 200.

Các nhánh cần tự thử thêm:

| Case | Thao tác | Mong đợi |
|---|---|---|
| H04-01 | Member gửi lời mời A | 403/BR-08 |
| H04-02 | Mời lại người đã là thành viên | 409/ALREADY_MEMBER |
| H04-03 | Gửi lại lời mời còn Pending cho cùng user | 409/ALREADY_INVITED |
| H04-04 | Mời username không có trong directory | 404; kiểm đồng bộ trước nếu user vừa được tạo |
| H04-05 | Invite user tạm đang bị khóa | Bị từ chối; implementation hiện tại trả 400/BR-03 |
| H04-06 | Người được mời POST `.../decline` | 200/Declined; không thêm membership |

### 6.2 BR-07: không vượt giới hạn nhóm

Dùng project C mới để không làm Student3 trở thành thành viên A, phá dữ liệu bài phân quyền.
Chuẩn bị Student2, Student3, Student4 và hai user tạm đã đăng ký/đồng bộ directory, đều ở role Student và không bị khóa.

1. Student1 tạo C: hiện có 1 Leader.
2. Gửi bốn invitation Pending hợp lệ tới bốn user khác nhau: tổng slot là 1 thành viên + 4 lời mời Pending.
3. Mời user thứ năm ngoài nhóm → 409/BR-07.
4. Không dùng invitation trùng username cho bước 3, vì sẽ chỉ kiểm ALREADY_INVITED.
5. Gán Lecturer không được làm tăng số slot Student; đối chiếu membership/role thực tế.

TASKS H-04 tính cả lời mời Pending còn hiệu lực; source hiện tại không tính Lecturer trong số thành viên Student.

### 6.3 Gán giảng viên và BR-10

1. Leader A GET `/api/directory/users?role=Lecturer`; lấy Guid Lecturer1.
2. PUT `/api/projects/<A>/lecturer`, body `{ "lecturerId": "<LECTURER_A_ID>" }` → 200.
3. GET detail/members A: lecturerId đúng, membership Lecturer được cập nhật.
4. Lecturer1 thấy A trong danh sách project; sau đồng bộ đọc được Diagram/Review của A.
5. Thử gán Student chưa đổi role Lecturer → 400/BR-10.
6. Member A thử gán giảng viên → 403.
7. Leader B gán Lecturer2 cho B. Lecturer2 vẫn không được duyệt A.
8. Cuối bài tích hợp, đổi Lecturer A sang Lecturer2; chờ Lecturer2 có quyền, Lecturer1 mất quyền A.

### 6.4 BR-09 và job hết hạn

- Invitation mới phải có `ExpiresAt = CreatedAt + 7 ngày`.
- Không cần chờ 7 ngày hoặc sửa SQL dùng chung: chạy BR09_InvitationExpirationTests để kiểm fixture thời gian cũ và kết quả 410/BR-09.
- Admin GET `/api/admin/workspace/jobs` → có `expire-invitations`, interval 1 giờ.
- POST `/api/admin/workspace/jobs/expire-invitations/run` → 202/JobRunResult, succeeded true.
- Không có lời mời hết hạn thì summary đếm 0 vẫn hợp lệ.

Tách case “Pending đã hết hạn” khỏi case job đã chuyển status sang Expired. Theo source hiện tại,
accept một invitation không còn Pending có thể nhận 400/INVALID_STATUS trước khi kiểm thời gian.
Nếu cần nghiệm thu mọi invitation hết hạn đều phải trả 410 theo PRD, ghi nhận khác biệt này cho Workspace;
không tự sửa tiêu chí mong đợi thành 400 rồi đánh dấu BR-09 hoàn toàn PASS.

## 7. H-05 — Gateway

Chạy các bài này sau khi auth/project hoạt động, nhưng **trước** hoặc tách riêng bài rate limit để tránh 429 gây nhiễu.

| Case | Thao tác qua `localhost:5000` | Mong đợi |
|---|---|---|
| H05-01 | Login, GET users/me | Response của Identity, không phải 502 |
| H05-02 | GET projects, directory, invitations bằng đúng actor | Tới Workspace, dữ liệu đúng user |
| H05-03 | GET diagrams/reviews sau khi tạo dữ liệu tích hợp | Tới Diagram, không lỗi route Workspace |
| H05-04 | GET `/api/admin/identity/jobs` bằng Admin | Tới shared job endpoint Identity |
| H05-05 | Gọi route cần login khi thiếu/sai token | 401; `/api/auth/login` vẫn gọi được khi anonymous |
| H05-06 | Gửi header `X-Correlation-Id: selftest-<RUN_ID>` | Response giữ header; tìm được cùng ID trong log Gateway/upstream |
| H05-07 | Không gửi CorrelationId | Gateway tạo một giá trị và trả qua header |
| H05-08 | GET `/swagger` | 200; portal có link 5 Swagger service |

Docker phải dùng `identity:8080`, `workspace:8080`… làm upstream. `localhost` trong healthcheck của chính
container là đúng; không đánh nhầm nó là upstream sai. Dev `dotnet run` dùng localhost qua Development config.

**CORS:** gửi OPTIONS tới route API với `Origin: http://localhost:5173` và
`Access-Control-Request-Method: GET`; kiểm `Access-Control-Allow-Origin` đúng origin.
Lặp với origin ngoài danh sách → không được cấp header allow-origin. CORS là chính sách trình duyệt;
không yêu cầu một GET từ công cụ HTTP ngoài trình duyệt phải trả 403 chỉ vì Origin lạ.

**Rate limit:** `/api/auth/login` tối đa 10 request/phút/IP theo H-05, không phải theo username.
Sau khi đã login nhiều lần, bucket có thể đã được dùng. Muốn kiểm ngưỡng rõ, để qua cửa sổ giới hạn rồi
gửi 11 request login trong cùng một phút, cùng đường truy cập/IP: có 429 khi vượt ngưỡng.
Chạy bài này cuối buổi; khi gặp 429 ở bài khác, chờ hết cửa sổ thay vì sửa auth hoặc tắt limiter.

## 8. H-06 — UI của Huy và chuẩn DESIGN

Mở DevTools → Network, bật Preserve log để đối chiếu URL/status. Không xuất HAR có Authorization/token cho người khác.

| Màn hình | Thao tác tay | Điều cần thấy |
|---|---|---|
| UI-01 `/login` | Sai mật khẩu, user bị khóa, login đúng, đăng xuất | Lỗi rõ; không treo loading; đúng user; đăng xuất không truy cập tiếp trang cần login |
| UI-02 `/register` | Trống trường, mật khẩu ngắn, username trùng, đăng ký hợp lệ | Lỗi theo trường; không tạo user ở case sai |
| UI-03 `/projects` | Search/filter/sort/page; danh sách rỗng | Query thay đổi đúng; không lẫn project người khác; có trạng thái empty |
| UI-04 `/projects/<A>` | Tab Thành viên/Sơ đồ/Review; đổi A sang B | Header và request cùng project; không dùng GUID demo hoặc dữ liệu cũ của A trong B |
| UI-05 Mời thành viên | Leader mời; Member mở lại project | Tra directory đúng; role theo membership; xử lý 403/409 rõ ràng |
| `/invitations/mine` | Student2 xem/accept/decline lời mời | Đúng người nhận; cập nhật sau thao tác, không cần sửa SQL |
| UI-10 `/admin/users` | Admin search/filter/page/khóa/đổi role; Student truy cập URL trực tiếp | Admin dùng được; Student không thao tác quản trị được |
| Điểm nối UI-11 `/admin/render` | Chạy job Identity/Workspace | Danh sách job, loading, kết quả/lastResult; API đúng service |

### 8.1 Checklist DESIGN cho từng màn đang test

- [ ] Loading có dấu hiệu rõ, submit đang chạy bị disable.
- [ ] Empty có giải thích/hành động; không coi empty là lỗi tải.
- [ ] Error nói rõ lỗi và hướng xử lý; lỗi form liên kết với trường, có thử lại khi phù hợp.
- [ ] Success cập nhật dữ liệu và có phản hồi; Diagram/Review dùng toast thay `alert()`.
- [ ] Toast góc phải dưới, `aria-live="polite"`, có nút đóng, tự ẩn sau khoảng 4 giây.
- [ ] Status diagram có badge Pending/Rendering/Rendered/Failed tương ứng dữ liệu API.
- [ ] Mọi input có nhãn; icon-only button có aria-label; focus nhìn thấy rõ khi dùng bàn phím.
- [ ] SVG có role img và tên sơ đồ; ngày giờ hiển thị theo quy ước DESIGN.
- [ ] Không có thao tác “thành công” giả trong UI khi API trả lỗi.

Checklist là tiêu chí để tìm lỗi theo DESIGN; không mặc định tất cả màn đã đạt chỉ vì production build thành công.

### 8.2 Modal tạo diagram — kiểm cả hai điểm mở

Thử tại tab Sơ đồ của ProjectDetail A và tại `/diagrams?projectId=<A>`:

1. Mở “Tạo sơ đồ mới”: focus vào tiêu đề, dialog có tên và nhãn từng trường.
2. Tab/Shift+Tab: focus ở trong dialog; Esc đóng và trả focus về nút mở.
3. Model JSON sai cú pháp: lỗi tại form, trường có aria-describedby; không gửi POST tạo diagram.
4. JSON hợp lệ nhưng API từ chối: dialog giữ nội dung nhập, cho sửa/thử lại.
5. Bật network throttling để nhìn trạng thái đang tạo: không gửi trùng; Esc/Hủy không đóng giữa request.
6. Tạo thành công: chuyển tới diagram đúng A và còn toast thành công.

Không áp dụng điều kiện chặn Esc lúc đang gửi để kết luận modal lúc rảnh cũng được phép không đóng.

### 8.3 Refresh interceptor bằng phiên thật

Sau login bằng một actor test, có thể tạo tình huống access token sai nhưng refresh token vẫn thật.
Trong DevTools Console của chính origin client đang test, chạy đoạn sau; nó chỉ sửa session local của tab:

```javascript
(() => {
  const key = 'umlforge.session.v2';
  const saved = JSON.parse(sessionStorage.getItem(key));
  if (!saved) throw new Error('Cần đăng nhập bằng tài khoản test trước.');
  sessionStorage.setItem(key, JSON.stringify({
    ...saved,
    accessToken: 'self-test-invalid-access-token',
    expiresAt: Date.now() + 60000
  }));
})();
```

Nên thực hiện khi đang ở chi tiết project hoặc `/invitations/mine`, sau đó bấm link Dự án để đổi route và phát sinh
request. Bấm lại link Dự án khi đã đứng ở đúng trang đó có thể không tạo request mới; phải kiểm tra Network:

- Request đầu 401 → có một POST `/api/auth/refresh` → retry request gốc thành công, vẫn đúng user.
- Khi nhiều request cùng 401, không gửi nhiều refresh song song cho cùng phiên.
- Không lặp 401 → refresh → retry vô hạn; refresh URL không tự retry refresh chính nó.
- Thử reload trang trong trạng thái access token sai: `/users/me` phục hồi, không treo “đang kiểm tra phiên”.

Để test refresh lỗi, login một phiên test mới, làm tương tự nhưng đặt thêm
`refreshToken: 'self-test-invalid-refresh-token'`; request 401 và refresh lỗi phải đưa về đăng nhập/xóa phiên local.
Login lại sau case này. Không làm bài rotation thủ công bằng chính refresh token đang được UI sử dụng.

Race “request cũ không ghi đè/xóa phiên mới” được kiểm ổn định bằng `session-api.test.mjs` ở mục 10;
thao tác tay trên mạng nhanh không đủ để kết luận race đã được cover.

## 9. H-07 — Packaging, migration, Swagger

### 9.1 Build image đúng context và revision

Chạy từ repo root, không chạy với context chỉ nằm trong thư mục Identity.API:

```powershell
$revision = git rev-parse HEAD
docker build --progress plain --build-arg "REVISION=$revision" -f src/Services/Identity/Identity.API/Dockerfile -t umlforge-selftest-identity src
docker build --progress plain --build-arg "REVISION=$revision" -f src/Services/Workspace/Workspace.API/Dockerfile -t umlforge-selftest-workspace src
docker build --progress plain -f src/Gateway/UMLForge.Gateway/Dockerfile -t umlforge-selftest-gateway src
```

Mong đợi restore/build/publish thành công; Identity/Workspace có log revision khớp SHA đã truyền;
`Directory.Build.props` được COPY trước restore. Không dùng image build lỗi hoặc tag cũ để nghiệm thu source mới.

### 9.2 Schema và lỗi startup

- E2E runner ở mục 11 dùng SQL Server mới, chứng minh schema được tạo bằng migration trước khi API nghiệp vụ chạy.
- Với môi trường test tay: restart Workspace rồi kiểm project trước/sau vẫn còn, không sinh schema/history trùng.
- Khi migration lỗi, Workspace phải ghi critical `Workspace schema migration failed; startup aborted.`,
  thoát khác 0 và không báo startup khỏe.

Có thể kiểm fail-fast kết nối DB bằng **container phụ** không có network, dùng image đã build ở mục 9.1:

```powershell
$fixtureName = 'umlforge-workspace-invalid-' + [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
docker run --name $fixtureName --network none -e ASPNETCORE_ENVIRONMENT=Production -e "ConnectionStrings__DefaultConnection=Server=127.0.0.1,1;Database=WorkspaceSelfTestInvalid;User Id=invalid;Password=invalid-test-only;TrustServerCertificate=True;Connect Timeout=2;ConnectRetryCount=0" umlforge-selftest-workspace
$migrationExitCode = $LASTEXITCODE
$migrationExitCode
docker inspect --format '{{.State.ExitCode}}' $fixtureName
docker rm $fixtureName
```

Mong đợi exit khác 0 và critical log; không có `Now listening on`. Đây là fixture lỗi kết nối trong bước migration,
không phải bằng chứng đã test mọi schema conflict. Nó không kết nối hoặc sửa WorkspaceDb đang dùng.
Kiểm schema không tương thích phải dùng database/stack fixture riêng, không sửa/xóa migration đã apply trên DB chung.

### 9.3 Swagger và API convention

- [ ] Swagger có mô tả request/response, status lỗi, Authorize; thử trực tiếp được.
- [ ] Id Guid, JSON camelCase, thời gian API UTC.
- [ ] GET list có phân trang/lọc/sort; PagedResult đúng shape.
- [ ] GET detail có ETag, PUT yêu cầu If-Match, conflict không ghi đè dữ liệu.
- [ ] Lỗi có ProblemDetails và code/errors phù hợp; ghi rõ nếu response middleware auth/rate-limit không có cùng shape.
- [ ] API Admin bị chặn ở backend, không chỉ ẩn nút frontend.
- [ ] Log không có password, JWT, refresh token hoặc connection string đầy đủ.

## 10. Chạy test tự động phần Huy

### 10.1 Build và unit tests

```powershell
dotnet build src/Services/Identity/Identity.sln
dotnet test src/Services/Identity/Identity.sln --no-build --filter Trait=BR
dotnet test src/Services/Identity/Identity.Tests/Identity.Tests.csproj --no-build --filter 'FullyQualifiedName~PurgeRefreshTokensJobTests|FullyQualifiedName~JobRegistryTests'
dotnet build src/Services/Workspace/Workspace.sln
dotnet test src/Services/Workspace/Workspace.sln --no-build --filter Trait=BR
```

Muốn kiểm đủ handler/job bên ngoài tập BR: chạy toàn bộ hai solution test, không dùng filter.
Đối với Diagram, trait dùng key `BR`, nên filter mục tiêu là
`BR=BR-11|BR=BR-29|BR=BR-30|BR=BR-32`; không áp đặt filter `Trait=BR` cho mọi project.

PASS phải có số test thực sự chạy > 0, Failed 0; xem cả Skipped và log discovery.
`No test matches`, DLL bị Application Control chặn (`0x800711C7`), hoặc exit code 0 nhưng không load được test assembly
**không được tính là test PASS**. Không xóa/Skip test để xử lý tình huống này.

Nếu Windows chặn DLL, có thể chạy test bằng SDK Linux có sẵn trong build stage:

```powershell
docker build --target build -f src/Services/Identity/Identity.API/Dockerfile -t umlforge-selftest-identity-sdk src
docker run --rm umlforge-selftest-identity-sdk dotnet test /src/Services/Identity/Identity.Tests/Identity.Tests.csproj --filter Trait=BR
docker run --rm umlforge-selftest-identity-sdk dotnet test /src/Services/Identity/Identity.Tests/Identity.Tests.csproj --filter 'FullyQualifiedName~PurgeRefreshTokensJobTests|FullyQualifiedName~JobRegistryTests'
docker build --target build -f src/Services/Workspace/Workspace.API/Dockerfile -t umlforge-selftest-workspace-sdk src
docker run --rm umlforge-selftest-workspace-sdk dotnet test /src/Services/Workspace/Workspace.Tests/Workspace.Tests.csproj --filter Trait=BR
```

Không đổi Windows Application Control hoặc antivirus policy để làm test chạy.

### 10.2 Client build và regression

Nếu chưa có node_modules, chạy `npm --prefix client ci` theo package-lock hiện có; không thêm thư viện mới.

```powershell
npm --prefix client run build
node --test client/tests/session-api.test.mjs
node client/tests/review-access.browser.mjs
node client/tests/diagram-feedback.browser.mjs
```

| Script | Chứng minh | Giới hạn |
|---|---|---|
| session-api.test.mjs | Refresh/retry, single-flight, phiên cũ/mới, timeout, projectId qua shared client | Fetch mock; không chứng minh server rotation/gRPC thật |
| review-access.browser.mjs | UI lấy quyền Leader/Lecturer theo membership, từ chối thiếu quyền, response A về chậm không cấp quyền B | Browser thật, API mock |
| diagram-feedback.browser.mjs | Toast, modal, bàn phím/focus, lỗi form, submit trùng, UI feedback | Browser thật, API mock |

Hai script browser tự mở Vite ở 5181/5182 và Edge debug ở 9230/9231, rồi dọn process của lượt test.
Chạy lần lượt; cần Edge, Node có WebSocket; có thể đặt `EDGE_PATH` nếu Edge nằm ở chỗ khác.
Không trộn kết quả những script này với bằng chứng E2E backend thật ở mục 11.

## 11. Từ phần Huy sang Diagram/Review/Render thật

### 11.1 Demo tay theo dữ liệu A/B đã chuẩn bị

1. Leader A mở `/projects/<A>` → Sơ đồ → tạo Use Case; dùng JSON hợp lệ mẫu bên dưới.
2. Xác nhận POST `/api/diagrams` mang projectId A, URL view giữ A; không có GUID demo.
3. Trước render, Leader gửi duyệt → 400/BR-29. Member không có nút gửi duyệt và gọi API trực tiếp cũng bị từ chối.
4. Render diagram: POST trả 202; UI Pending/Rendering rồi Rendered; thấy SVG thật, polling dừng khi kết thúc.
5. Nếu A có hai diagram, để một cái Draft: gửi duyệt vẫn phải bị từ chối, không chỉ kiểm diagram vừa mở.
6. Render xong mọi diagram A, Leader POST `/api/reviews` `{ "projectId": "<A>" }` → 201/Submitted.
7. Lecturer A xem/comment được; không tạo/sửa/xóa/render/restore diagram (403/BR-32 khi gọi API).
8. Lecturer B mở A hoặc quyết định Review A → 403; Leader/Member A cũng không được approve thay Lecturer A.
9. Lecturer A request-changes hoặc approve đúng review đang Submitted → 200, trạng thái cập nhật.
10. Xuất ZIP bằng UI → file có SVG/PlantUML/JSON của A, không lẫn B.
11. Student3 mở B: chỉ diagram B. Thử gọi API diagram/review A bằng Student3 → 403/BR-11.
12. Cuối cùng xóa Member A hoặc đổi Lecturer qua Workspace, đợi consumer đồng bộ rồi kiểm quyền người cũ bị thu hồi.

JSON mẫu dùng cho Use Case, dựa trên model/parser hiện tại:

```json
{
  "title": "CampusEats Ordering",
  "systemBoundary": "CampusEats",
  "actors": ["Student"],
  "useCases": ["Order Food"],
  "relations": [
    { "from": "Student", "to": "Order Food", "type": "association" }
  ]
}
```

Body tạo diagram chứa projectId thật, `type: 2`, title và **modelJson là chuỗi JSON**, không phải object lồng.
Trong modal có thể dán JSON trực tiếp vào textarea; API client sẽ bọc thành chuỗi.

### 11.2 E2E tự động với 11 container thật

```powershell
python -u tests/IntegrationTests/verify_diagram_review.py --output ../.antigravity/my-self-test
```

Đọc thêm [README của runner](../tests/IntegrationTests/README.md).
Runner dùng source hiện tại, seed user trong SQL riêng, JWT thật, Gateway/nginx thật, Redis Streams,
DiagramEngine gRPC và PlantUML thật. Không dùng `.env` thật, dev MemberAccess endpoint, SQL write hoặc sự kiện giả.
Nó tự tạo/dọn container/network riêng, không xóa volume hoặc container Compose đang dùng.

**Điều kiện PASS:** lệnh kết thúc thành công, có dòng `PASS all checks`, `result.json` có `status: passed` và đúng
sourceRevisions. Xem cả `browser.log`, log build và log từng container trong thư mục output.
`--skip-build` chỉ dùng lại image khi source không đổi; sau sửa source phải build lại.

Runner đã cover các nhánh trọng tâm BR-11/29/30/32 và thu hồi quyền. Không dùng nó để tự nhận rằng
mọi BR, cả năm loại UML, recovery/DLQ, kiểm tra tải hoặc lệnh Compose chung đã được nghiệm thu hoàn toàn.

### 11.3 Bàn giao kiểm tra tiếp cho đồng đội

| Chủ phần tiếp theo | Nối từ kết quả của Huy | Phần cần tự kiểm sâu theo TASKS |
|---|---|---|
| Thiện — T-01…07 | ProjectCreated/MemberAdded/LecturerAssigned đã tới Diagram; JWT thật | CRUD/version/ETag, BR-11…17, gRPC validation, Review/comment/ZIP, BR-29…32, UI-07/08 |
| Minh — M-01…07 | Diagram phát RenderRequested; user/project thật | Worker, timeout, giữ SVG cũ, retry/reconcile/DLQ, báo cáo ngày, Notification recipients, BR-26…28/33…35, UI-09/11 |
| Bảo — B-01…07 | Các service và UI đang dùng shared contract | Outbox/Inbox/idempotency, bốn RPC và năm loại UML, shared polling, Compose/CI/tools, UI-06 |

Lệnh build/test độc lập khi đến phần người đó:

```powershell
dotnet build src/Services/Diagram/Diagram.sln
dotnet test src/Services/Diagram/Diagram.sln --no-build
dotnet build src/Services/DiagramEngine/DiagramEngine.sln
dotnet test src/Services/DiagramEngine/DiagramEngine.sln --no-build
dotnet build src/Services/Render/Render.sln
dotnet test src/Services/Render/Render.sln --no-build
dotnet build src/Services/Notification/Notification.sln
dotnet test src/Services/Notification/Notification.sln --no-build
```

Một số integration tests cần Docker và thời gian chờ container. Chỉ đánh dấu I-01…I-06 khi có bằng chứng cho
đúng kịch bản trong TASKS; không lấy một lượt build/unit xanh để thay thế toàn bộ buổi ghép nhóm.

## 12. Khi lỗi, khoanh vùng ở đâu?

| Hiện tượng | Kiểm tra trước | Không vội kết luận |
|---|---|---|
| Login 401 | Đúng password cấu hình lúc seed, đúng user, account có tồn tại | Không reset DB hoặc đổi JWT key để chữa sai mật khẩu |
| Login 403 | IsLocked và BR-03 | Không sửa Gateway cho qua auth |
| Login 429 | Bucket login theo IP; chờ hết cửa sổ | Không coi mọi 429 là password sai |
| Qua service trực tiếp được nhưng Gateway 502 | Gateway Docker config, DNS/upstream, health | Không đổi Docker upstream thành localhost |
| Mọi service từ chối JWT | Key/SecretKey, issuer, audience, thời hạn và đúng actor | Không dán token/secret vào log nhờ kiểm tra |
| User mới chưa có trong directory | Identity Outbox/log, stream identity.events, Workspace consumer/Inbox | Không tự insert chéo DB |
| Member vừa accept nhưng Diagram 403 | Workspace Outbox, workspace.events, Diagram consumer/MemberAccess | Không bỏ check membership hoặc dùng dev access endpoint làm bằng chứng E2E |
| PUT project 409 | ETag/If-Match, bản đã được sửa ở nơi khác | Không bỏ optimistic concurrency |
| Diagram Failed sau 202 | Render job/error, engine gRPC, PlantUML, render.events | 202 chỉ xác nhận nhận yêu cầu; chưa chứng minh render thành công |
| Review 400/BR-29 | Tất cả diagram chưa xóa của project, không chỉ diagram đang mở | Không ép Status=Rendered trong SQL |
| Jobs 202 nhưng succeeded false | summary và log job/service | Không đánh PASS chỉ dựa trên status HTTP |
| UI treo loading sau refresh | Network /users/me, /auth/refresh, retry và phiên hiện tại | Không lặp refresh vô hạn hoặc xóa phiên mới bởi request cũ |
| Unit test báo không load DLL/no tests | Application Control, filter trait, số test thật sự chạy | Không coi exit 0 hoặc “No test matches” là PASS |

## 13. Ghi biên bản và chốt phần của mình

Mỗi lượt tạo một bản ghi riêng, ví dụ ngoài repository tại `../.antigravity/self-test-<RUN_ID>.md`:

```text
Ngày/giờ:
Người test:
Branch / SHA:
Môi trường: Compose chung / runner E2E riêng / dotnet run
Case | PASS / FAIL / NOT RUN | status/code thực tế | ảnh/log/CorrelationId | ghi chú
H01-01 | ...
H02-01 | ...
H03-01 | ...
H04-01 | ...
H05-01 | ...
UI-04 | ...
E2E BR-11/29/30/32 | ...
```

- [ ] H-01…H-07 có kết quả thực tế và evidence; case thiếu dữ liệu ghi NOT RUN, không tự điền PASS.
- [ ] Có cả nhánh thành công và từ chối phân quyền/validation/concurrency.
- [ ] Actor, Guid project/diagram/review trong ảnh và request khớp nhau.
- [ ] Phân biệt unit/mock browser/E2E thật; ghi đúng phạm vi đã kiểm tra.
- [ ] Không đưa password, Authorization, JWT/refresh token hoặc connection string thật vào ảnh/biên bản.
- [ ] Ghi lỗi cho đúng owner; không sửa ngoài phạm vi hoặc đổi contract chỉ để test xanh.
- [ ] Khi cập nhật PROGRESS, ghi SHA, số tests thật sự chạy, giới hạn và phần còn NOT RUN; không chỉ ghi “100%”.

Dừng môi trường test tay khi không dùng:

```powershell
docker compose --env-file deploy/.env -f deploy/docker-compose.yml stop
```

Lệnh này giữ container/dữ liệu để chạy lại. Với runner E2E riêng, cleanup đã do runner thực hiện.
