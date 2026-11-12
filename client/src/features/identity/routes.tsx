import type { ReactElement } from 'react';

// Huy (H-06) — UI-01, UI-02, UI-10. Mỗi <Route> cần key, path KHÔNG có dấu "/" ở đầu.
// Ví dụ:
//   identityPublicRoutes: <Route key="login" path="login" element={<LoginPage />} />
//   identityRoutes:       <Route key="admin-users" path="admin/users" element={<AdminUsersPage />} />

/** Trang không cần đăng nhập (đăng nhập, đăng ký). */
export const identityPublicRoutes: ReactElement[] = [];

/** Trang cần đăng nhập, hiển thị trong AppLayout. */
export const identityRoutes: ReactElement[] = [];
