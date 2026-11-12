# DESIGN — UMLForge v2

> Màu sắc, font chữ, các component và phong cách giao diện.
> Kế thừa design token đang dùng ở `client/src/styles.css` (v1) để giữ nhận diện, chỉ bổ sung phần còn thiếu cho v2.

---

## 1. Nguyên tắc giao diện

| # | Nguyên tắc | Ý nghĩa thực tế |
|---|---|---|
| D1 | **Công cụ làm việc, không phải landing page** | Bố cục gọn, ít trang trí, mật độ thông tin vừa phải |
| D2 | **Trạng thái luôn nhìn thấy** | Diagram nào đang Pending/Rendering/Rendered/Failed phải thấy ngay bằng badge màu |
| D3 | **Lỗi phải chỉ ra chỗ sai** | Lỗi validate chỉ rõ đường dẫn trong JSON (`$.classes[2].name`), không chỉ báo "Invalid" |
| D4 | **Một hành động chính trên một màn hình** | Chỉ 1 nút `button-primary` mỗi vùng (Lưu, Render, Duyệt…) |
| D5 | **Tiếng Việt cho người dùng, tiếng Anh cho thuật ngữ UML** | "Lưu sơ đồ", "Class Diagram", "Render" |

---

## 2. Màu sắc (Design Tokens)

Khai báo trong `client/src/styles/tokens.css` dưới dạng CSS variables.

### 2.1 Màu nền và chữ
| Token | Giá trị | Dùng cho |
|---|---|---|
| `--color-bg` | `#f6f7fb` | Nền trang |
| `--color-surface` | `#ffffff` | Panel, card, modal, input |
| `--color-text` | `#202735` | Chữ chính |
| `--color-text-secondary` | `#344055` | Chữ nút phụ, nhãn |
| `--color-muted` | `#697386` | Mô tả, chú thích |
| `--color-muted-2` | `#8a94a6` | Placeholder, thời gian |
| `--color-border` | `#e0e5ef` | Viền panel, bảng |
| `--color-border-strong` | `#d9dfea` | Viền input |
| `--color-border-soft` | `#e5e9f1` | Đường kẻ phân cách |

### 2.2 Màu thương hiệu
| Token | Giá trị | Dùng cho |
|---|---|---|
| `--color-primary` | `#344bd0` | Nút chính, link, tab đang chọn |
| `--color-primary-hover` | `#273dbb` | Hover nút chính |
| `--color-primary-tint` | `#eef2ff` | Nền chip/tab đang chọn, hàng được chọn |
| `--color-focus` | `#8b9aff` | Outline khi focus (2px) |

### 2.3 Màu ngữ nghĩa
| Token | Chữ | Nền | Viền | Dùng cho |
|---|---|---|---|---|
| `--color-danger` | `#a12d3b` | `#fff1f1` | `#f4cfd5` | Lỗi, xóa, Failed |
| `--color-warning` | `#8a5a00` | `#fff7e6` | `#f3dcae` | Cảnh báo, Pending |
| `--color-success` | `#1f7a4d` | `#ecf8f1` | `#bfe5cf` | Rendered, Approved *(mới ở v2)* |
| `--color-info` | `#344bd0` | `#eef2ff` | `#cdd6ff` | Rendering, thông tin *(mới ở v2)* |

### 2.4 Badge trạng thái
| Trạng thái | Nhãn hiển thị | Màu | Icon |
|---|---|---|---|
| `Draft` | Nháp | muted | ✎ |
| `Pending` | Chờ render | warning | ⏳ |
| `Rendering` | Đang render… | info | ⟳ (xoay) |
| `Rendered` | Đã render | success | ✓ |
| `Failed` | Render lỗi | danger | ✕ |
| Review `Submitted` | Chờ duyệt | warning | |
| Review `Approved` | Đã duyệt | success | |
| Review `ChangesRequested` | Cần sửa | danger | |

> Không chỉ dùng màu để truyền tải trạng thái — luôn có chữ hoặc icon đi kèm (khả năng tiếp cận).

---

## 3. Typography

| Token | Giá trị |
|---|---|
| `--font-sans` | `'Segoe UI', system-ui, -apple-system, sans-serif` |
| `--font-mono` | `ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace` |

| Cấp | Cỡ / dòng | Độ đậm | Dùng cho |
|---|---|---|---|
| `h1` | 24 / 32px | 600 | Tiêu đề trang |
| `h2` | 18 / 26px | 600 | Tiêu đề panel |
| `h3` | 15 / 22px | 600 | Tiêu đề nhóm trong panel |
| `body` | 14 / 22px | 400 | Nội dung chung |
| `small` | 12 / 18px | 400 | Chú thích, thời gian, badge |
| `code` | 13 / 20px mono | 400 | JSON editor, PlantUML preview |

---

## 4. Khoảng cách, bo góc, bóng đổ

| Token | Giá trị | Ghi chú |
|---|---|---|
| `--space-1 … --space-8` | 4, 8, 12, 16, 20, 24, 32, 48px | Bội số của 4 |
| `--radius-sm` | 6px | Badge, chip |
| `--radius-md` | 8px | Nút, input, panel |
| `--radius-lg` | 10px | Logo, modal |
| `--shadow-sm` | `0 1px 2px rgba(32,39,53,.06)` | Panel |
| `--shadow-md` | `0 8px 24px rgba(32,39,53,.12)` | Modal, dropdown, toast |
| Chiều cao nút | min 44px (nhỏ: 32px) | Đủ lớn để bấm |
| Chiều cao input | min 48px | |

---

## 5. Bố cục (Layout)

```
┌──────────────────────────────────────────────────────────────┐
│ Topbar: logo · breadcrumb · 🔔 thông báo · avatar ▾          │ 56px
├────────────┬─────────────────────────────────────────────────┤
│ Sidebar    │ Nội dung trang                                   │
│ 240px      │  max-width 1280px, padding 24px                  │
│ - Dự án    │                                                  │
│ - Review   │                                                  │
│ - Admin    │                                                  │
└────────────┴─────────────────────────────────────────────────┘
```

| Breakpoint | Hành vi |
|---|---|
| ≥ 1200px | Sidebar mở, editor chia 2 cột (JSON trái · preview phải) |
| 768–1199px | Sidebar thu gọn thành icon, editor chia 2 tab |
| < 768px | Sidebar thành menu trượt, chỉ xem/duyệt (không khuyến khích chỉnh JSON trên điện thoại) |

---

## 6. Component

| Component | Class / file | Biến thể | Ghi chú |
|---|---|---|---|
| Button | `.button` | `primary`, `secondary`, `danger`, `ghost`, `small` | `:disabled` → `cursor: not-allowed`, opacity .55 |
| Input / Select / Textarea | `.input` | `error` | Nhãn phía trên, lỗi phía dưới màu danger |
| Panel | `.panel`, `.panel-header` | | Nền trắng, viền `--color-border`, bo 8px |
| Status badge | `.status-badge.status-{x}` | 8 trạng thái mục 2.4 | |
| Tabs | `.tabs`, `.tab-btn` | | Tab chọn: chữ primary + gạch chân 2px |
| Chip | `.chip-btn` | `active` | Chọn loại diagram, template |
| Data table | `.data-table` | | Header dính, hàng hover `--color-primary-tint` |
| Filters bar | `.filters-bar` | | Ô tìm kiếm + select lọc + sắp xếp |
| Pagination | `.pagination` *(mới)* | | « 1 2 3 … » · "Hiển thị 1–20 / 134" |
| Modal | `.modal` | `confirm`, `form` | Đóng bằng Esc; nút nguy hiểm đặt bên phải |
| Toast | `.toast` *(mới)* | success/error/info | Góc phải dưới, tự ẩn sau 4 giây |
| Notice | `.notice` | warning/danger/info | Thông báo trong trang |
| Empty state | `.empty-state` | | Icon + 1 câu + 1 nút hành động |
| Skeleton | `.skeleton` *(mới)* | | Thay spinner khi tải danh sách |
| Code editor | `.code-textarea` + `.json-status-bar` | | Hiển thị dòng/cột lỗi JSON |
| SVG canvas | `.svg-canvas` + `.toolbar` | | Zoom +/−, vừa khung, tải SVG/PNG |
| Render status | `.status-badge` + spinner | | Tự cập nhật bằng polling 2 s khi Pending/Rendering, dừng khi Rendered/Failed |
| Notification bell | `.notif-bell` *(mới)* | | Badge số chưa đọc (hỏi lại mỗi 30 s), dropdown 10 mục gần nhất, bấm mục → mở link liên quan |
| Comment thread | `.comment-list` *(mới)* | | Dùng trong màn duyệt |
| Example panel | `.example-guide`, `.template-chips` | | Kế thừa v1 (skeleton + example + hướng dẫn) |

---

## 7. Trạng thái giao diện bắt buộc

Mọi màn hình có dữ liệu phải xử lý đủ 4 trạng thái:

| Trạng thái | Cách hiển thị |
|---|---|
| **Loading** | Skeleton (danh sách), spinner nhỏ trong nút (khi submit, nút bị disable) |
| **Empty** | `.empty-state`: "Chưa có sơ đồ nào. [+ Tạo sơ đồ]" |
| **Error** | `.notice.danger` + nút "Thử lại"; lỗi 403 → "Bạn không có quyền…"; 409 → "Dữ liệu đã bị người khác thay đổi, tải lại?" |
| **Success** | Toast success, cập nhật danh sách ngay, không bắt người dùng tải lại trang |

---

## 8. Danh sách màn hình (UI codes)

| Mã | Màn hình | Vai trò | BR liên quan |
|---|---|---|---|
| UI-01 | Đăng nhập | Tất cả | BR-03, BR-05 |
| UI-02 | Đăng ký | Khách | BR-01, BR-02 |
| UI-03 | Danh sách dự án (tìm/lọc/sắp xếp/phân trang) | Student, Lecturer | BR-06, BR-11 |
| UI-04 | Chi tiết dự án — tab Sơ đồ / Thành viên / Review | Student, Lecturer | BR-07, BR-08, BR-10 |
| UI-05 | Mời thành viên (modal) | Team Leader | BR-07, BR-08, BR-09 |
| UI-06 | Trình soạn sơ đồ: JSON + ví dụ + preview | Student | BR-12, BR-13, BR-15, BR-16, BR-18 → BR-25 |
| UI-07 | Xem sơ đồ + lịch sử phiên bản + tiến độ render | Student, Lecturer | BR-14, BR-15, BR-17, BR-28 |
| UI-08 | Gửi duyệt / màn duyệt + bình luận | Team Leader, Lecturer | BR-29 → BR-32 |
| UI-09 | Trung tâm thông báo (lọc chưa đọc, đánh dấu tất cả đã đọc) | Tất cả | BR-33, BR-34, BR-35 |
| UI-10 | Admin — Quản lý người dùng | Admin | BR-03, BR-04 |
| UI-11 | Admin — Giám sát render, báo cáo ngày, nút "Chạy ngay" cho job định kỳ (NV-08) | Admin | BR-26, BR-27, BR-28 |
| UI-12 | Trang lỗi 403 / 404 | Tất cả | |

> Mã UI-xx dùng chung trong slide, TASKS.md và traceability matrix.

---

## 9. Văn phong (Tone of voice)

| Nên | Không nên |
|---|---|
| "Đã lưu phiên bản 3." | "Thao tác thành công!!!" |
| "Tên class bị trùng: `Order` (dòng 12)." | "Lỗi dữ liệu." |
| "Sơ đồ đang render, bạn có thể tiếp tục làm việc." | "Vui lòng chờ…" |
| "Xóa sơ đồ này? Bạn có thể khôi phục trong 7 ngày." | "Bạn có chắc không?" |

- Câu ngắn, chủ động, nói rõ điều gì đã xảy ra và người dùng nên làm gì tiếp.
- Ngày giờ hiển thị theo `dd/MM/yyyy HH:mm` (múi giờ Asia/Ho_Chi_Minh); thời gian gần dùng dạng "2 phút trước".

---

## 10. Khả năng tiếp cận (Accessibility)

- Độ tương phản chữ/nền ≥ 4.5:1 (các màu ở mục 2 đã đạt).
- Mọi phần tử tương tác dùng được bằng bàn phím; focus thấy rõ (`outline: 2px solid var(--color-focus)`).
- Icon-only button phải có `aria-label`.
- SVG sơ đồ có `role="img"` và `aria-label` = tên sơ đồ.
- Toast dùng `aria-live="polite"`; lỗi form dùng `aria-describedby`.
