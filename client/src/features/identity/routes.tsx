import type { ReactElement } from 'react';
import { Route } from 'react-router';
import { LoginPage } from './LoginPage';
import { RegisterPage } from './RegisterPage';
import { AdminUsersPage } from './AdminUsersPage';

/** Trang không cần đăng nhập (đăng nhập, đăng ký). */
export const identityPublicRoutes: ReactElement[] = [
  <Route key="login" path="login" element={<LoginPage />} />,
  <Route key="register" path="register" element={<RegisterPage />} />,
];

/** Trang cần đăng nhập, hiển thị trong AppLayout. */
export const identityRoutes: ReactElement[] = [
  <Route key="admin-users" path="admin/users" element={<AdminUsersPage />} />,
];
