import { NavLink, Outlet } from 'react-router';
import { useAuth } from '../auth/AuthContext';
import type { UserRole } from '../auth/AuthContext';
import { Brand } from './Brand';

const roleLabels: Record<UserRole, string> = { Student: 'Sinh viên', Lecturer: 'Giảng viên', Admin: 'Quản trị viên' };

const workspaceLinks = [
  { to: '/projects', label: 'Dự án' },
  { to: '/notifications', label: 'Thông báo' },
];

const adminLinks = [
  { to: '/admin/users', label: 'Người dùng' },
  { to: '/admin/render', label: 'Giám sát render' },
];

export function AppLayout() {
  const { user, signOut } = useAuth();
  const links = user?.role === 'Admin' ? [...workspaceLinks, ...adminLinks] : workspaceLinks;
  return <div className="app-shell">
    <a className="skip-link" href="#main-content">Bỏ qua tới nội dung</a>
    <aside className="sidebar">
      <Brand />
      <p className="nav-label">KHÔNG GIAN LÀM VIỆC</p>
      <nav aria-label="Điều hướng chính">
        {links.map((link, index) => <NavLink key={link.to} to={link.to}
          className={({ isActive }) => isActive ? 'active' : undefined}>
          <span aria-hidden="true">{String(index + 1).padStart(2, '0')}</span>{link.label}
        </NavLink>)}
      </nav>
      <p className="sidebar-footer">Từ mô hình có cấu trúc<br />tới sơ đồ rõ ràng.</p>
    </aside>
    <div className="workspace">
      <header className="topbar">
        <span className="workspace-label">Không gian nhóm</span>
        <div className="account">
          {/* Chuông thông báo (M-06) gắn tại đây — 1 dòng */}
          <div className="account-name"><strong>{user?.fullName || user?.username}</strong>
            <span>{user ? roleLabels[user.role] : ''}</span></div>
          <button className="button button-secondary" onClick={signOut}>Đăng xuất</button>
        </div>
      </header>
      <main id="main-content" className="main-content"><Outlet /></main>
      <footer className="workspace-footer">UMLForge <span>Model · Validate · Render</span></footer>
    </div>
  </div>;
}
