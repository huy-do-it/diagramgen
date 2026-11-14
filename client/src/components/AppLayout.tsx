import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { NavLink, Outlet, useLocation } from 'react-router';
import { useAuth } from '../auth/AuthContext';
import type { UserRole } from '../auth/AuthContext';
import { Brand } from './Brand';
import { NotificationBell } from '../features/notification/NotificationBell';
import { ToastProvider } from '../shared/Toast';

const roleLabels: Record<UserRole, string> = { Student: 'Sinh viên', Lecturer: 'Giảng viên', Admin: 'Quản trị viên' };

const workspaceLinks = [
  { to: '/projects', label: 'Dự án', icon: 'projects' },
  { to: '/notifications', label: 'Thông báo', icon: 'notifications' },
] as const;

const adminLinks = [
  { to: '/admin/users', label: 'Người dùng', icon: 'users' },
  { to: '/admin/render', label: 'Giám sát render', icon: 'render' },
] as const;

type NavigationLink = (typeof workspaceLinks)[number] | (typeof adminLinks)[number];

function NavigationIcon({ name }: { name: NavigationLink['icon'] }) {
  return <svg className="navigation-icon" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    {name === 'projects' && <path d="M3 7V5a1 1 0 0 1 1-1h6l2 3h8a1 1 0 0 1 1 1v11a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V7Z" />}
    {name === 'notifications' && <><path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9M10 21h4" /></>}
    {name === 'users' && <><circle cx="9" cy="7" r="3" /><path d="M3 21v-3a6 6 0 0 1 12 0v3M16 4a3 3 0 0 1 0 6M21 21v-3a6 6 0 0 0-3-5" /></>}
    {name === 'render' && <path d="M3 12h4l3-8 4 16 3-8h4" />}
  </svg>;
}

function NavigationLinks({ links, onNavigate }: { links: readonly NavigationLink[]; onNavigate?: () => void }) {
  return <nav aria-label="Điều hướng chính">
    {links.map(link => <NavLink key={link.to} to={link.to} aria-label={link.label} title={link.label}
      onClick={onNavigate} className={({ isActive }) => isActive ? 'active' : undefined}>
      <NavigationIcon name={link.icon} /><span className="sidebar-link-label">{link.label}</span>
    </NavLink>)}
  </nav>;
}

function MobileNavigation({ links, onClose }: { links: readonly NavigationLink[]; onClose: () => void }) {
  const dialog = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const element = dialog.current!;
    const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const previousOverflow = document.body.style.overflow;
    element.showModal();
    document.body.style.overflow = 'hidden';
    element.querySelector<HTMLButtonElement>('button')?.focus();
    return () => {
      element.close();
      document.body.style.overflow = previousOverflow;
      if (opener?.isConnected && opener.getClientRects().length) opener.focus();
      else document.querySelector<HTMLElement>('.sidebar a')?.focus();
    };
  }, []);

  return createPortal(<dialog ref={dialog} id="mobile-navigation" className="navigation-drawer" aria-labelledby="mobile-navigation-title"
    aria-modal="true" onCancel={event => { event.preventDefault(); onClose(); }}
    onKeyDown={event => {
      if (event.key !== 'Tab') return;
      const items = [...event.currentTarget.querySelectorAll<HTMLElement>('button:not(:disabled), a[href]')]
        .filter(element => element.getClientRects().length > 0);
      const first = items[0];
      const last = items.at(-1);
      if (event.shiftKey && (document.activeElement === first || document.activeElement === event.currentTarget)) {
        event.preventDefault();
        last?.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first?.focus();
      }
    }}
    onClick={event => {
      if (event.target !== event.currentTarget) return;
      const bounds = event.currentTarget.getBoundingClientRect();
      if (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom) onClose();
    }}>
    <div className="drawer-heading">
      <h2 id="mobile-navigation-title">Menu điều hướng</h2>
      <button type="button" className="button button-ghost" aria-label="Đóng menu điều hướng" onClick={onClose}>×</button>
    </div>
    <Brand />
    <NavigationLinks links={links} onNavigate={onClose} />
  </dialog>, document.body);
}

export function AppLayout() {
  const { user, signOut } = useAuth();
  const location = useLocation();
  const [menuOpen, setMenuOpen] = useState(false);
  const links = user?.role === 'Admin' ? [...workspaceLinks, ...adminLinks] : workspaceLinks;
  useEffect(() => { setMenuOpen(false); }, [location.key]);
  useEffect(() => {
    const wideScreen = window.matchMedia('(min-width: 768px)');
    const closeOnResize = () => { if (wideScreen.matches) setMenuOpen(false); };
    wideScreen.addEventListener('change', closeOnResize);
    return () => wideScreen.removeEventListener('change', closeOnResize);
  }, []);
  return <div className="app-shell">
    <a className="skip-link" href="#main-content">Bỏ qua tới nội dung</a>
    <aside className="sidebar">
      <Brand />
      <p className="nav-label">KHÔNG GIAN LÀM VIỆC</p>
      <NavigationLinks links={links} />
      <p className="sidebar-footer">Từ mô hình có cấu trúc<br />tới sơ đồ rõ ràng.</p>
    </aside>
    <div className="workspace">
      <header className="topbar">
        <button type="button" className="button button-secondary mobile-menu-toggle" aria-label="Mở menu điều hướng"
          aria-expanded={menuOpen} aria-controls={menuOpen ? 'mobile-navigation' : undefined} onClick={() => setMenuOpen(true)}>
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true"><path d="M3 6h18M3 12h18M3 18h18" /></svg>
        </button>
        <span className="workspace-label">Không gian nhóm</span>
        <div className="account">
          <NotificationBell />
          <div className="account-name"><strong>{user?.fullName || user?.username}</strong>
            <span>{user ? roleLabels[user.role] : ''}</span></div>
          <button className="button button-secondary" onClick={signOut}>Đăng xuất</button>
        </div>
      </header>
      <main id="main-content" className="main-content"><ToastProvider><Outlet /></ToastProvider></main>
      <footer className="workspace-footer">UMLForge <span>Model · Validate · Render</span></footer>
    </div>
    {menuOpen && <MobileNavigation links={links} onClose={() => setMenuOpen(false)} />}
  </div>;
}
