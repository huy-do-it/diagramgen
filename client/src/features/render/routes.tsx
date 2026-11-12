import type { ReactElement } from 'react';

// Minh (M-06) — UI-11 giám sát render (chỉ Admin). Mỗi <Route> cần key.
// Ví dụ: <Route key="admin-render" path="admin/render" element={<AdminRenderPage />} />
export const renderRoutes: ReactElement[] = [];
