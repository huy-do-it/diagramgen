import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router';
import { useAuth } from '../../auth/AuthContext';
import { listNotifications, markAllRead, markRead, notificationLink, notificationTime, notifyRead, onRead } from './notificationApi';
import type { NotificationItem } from './notificationApi';
import type { PagedResult } from '../../shared/api';
import './notification.css';

export function NotificationsPage() {
  const { user } = useAuth();
  return user ? <NotificationList key={user.id} userId={user.id} /> : null;
}
function NotificationList({ userId }: { userId: string }) {
  const [page, setPage] = useState(1);
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [data, setData] = useState<PagedResult<NotificationItem> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false);
  const [revision, setRevision] = useState(0);
  const mutation = useRef<AbortController | null>(null);
  const navigate = useNavigate();
  useEffect(() => () => mutation.current?.abort(), []);
  useEffect(() => {
    if (!notice) return;
    const timer = window.setTimeout(() => setNotice(''), 4000);
    return () => window.clearTimeout(timer);
  }, [notice]);
  useEffect(() => onRead(userId, () => setRevision(x => x + 1)), [userId]);
  useEffect(() => {
    const controller = new AbortController(); setLoading(true); setError(''); setData(null);
    listNotifications(page, unreadOnly, controller.signal).then(result => {
      if (controller.signal.aborted) return;
      if (page > Math.max(1, result.totalPages)) { setPage(Math.max(1, result.totalPages)); return; }
      setData(result);
    }).catch((e: unknown) => { if (!controller.signal.aborted) setError(e instanceof Error ? e.message : 'Không tải được thông báo.'); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [page, unreadOnly, revision]);
  async function read(item?: NotificationItem) {
    if (busy) return;
    const controller = new AbortController(); mutation.current = controller; setBusy(true); setNotice(''); setError('');
    try {
      if (item) { if (!item.isRead) await markRead(item.id, controller.signal); }
      else await markAllRead(controller.signal);
      if (controller.signal.aborted) return;
      notifyRead(userId);
      if (item) navigate(notificationLink(item.link, item.type));
      else setNotice('Đã đánh dấu tất cả thông báo là đã đọc.');
    } catch (e) { if (!controller.signal.aborted) setError(e instanceof Error ? e.message : 'Không cập nhật được thông báo.'); }
    finally { if (!controller.signal.aborted) setBusy(false); }
  }
  return <section className="minh-notifications" aria-labelledby="notification-title">
    <header className="notif-page-heading"><div><p className="eyebrow">KHÔNG GIAN LÀM VIỆC</p><h1 id="notification-title">Thông báo</h1><p className="muted">Lời mời, góp ý và kết quả render của bạn.</p></div>
      <button type="button" className="button button-primary" disabled={busy || loading || !data?.totalItems} onClick={() => void read()}>{busy ? 'Đang cập nhật…' : 'Đánh dấu tất cả đã đọc'}</button></header>
    <div className="notif-toolbar"><label><input type="checkbox" checked={unreadOnly} disabled={busy} onChange={e => { setUnreadOnly(e.target.checked); setPage(1); setNotice(''); }} /> Chỉ chưa đọc</label><button type="button" className="button button-secondary" disabled={busy || loading} onClick={() => setRevision(x => x + 1)}>Làm mới</button></div>
    {notice && <p className="notif-success notif-toast" role="status" aria-live="polite">{notice}</p>}
    {error && <div className="notif-error" role="alert">{error}<button type="button" onClick={() => setRevision(x => x + 1)}>Thử lại</button></div>}
    {loading ? <div className="notif-skeleton" role="status">Đang tải thông báo…</div> : data && data.items.length === 0 ? <div className="notif-empty">{unreadOnly ? 'Bạn đã đọc hết thông báo.' : 'Bạn chưa có thông báo.'}</div> : data && <ul className="notif-list notif-page-list">
      {data.items.map(item => <li key={item.id}><button type="button" disabled={busy} className={item.isRead ? 'notif-entry' : 'notif-entry notif-unread'} onClick={() => void read(item)}>
        <span>{!item.isRead && <span className="notif-dot" aria-label="Chưa đọc">● </span>}{item.title}</span><time dateTime={item.createdAt}>{notificationTime(item.createdAt)}</time><span>{item.isRead ? 'Đã đọc' : 'Chưa đọc'} · Mở →</span>
      </button></li>)}
    </ul>}
    {data && data.totalItems > 0 && <nav className="notif-pagination" aria-label="Phân trang thông báo"><span>Hiển thị {(data.page - 1) * data.pageSize + 1}–{Math.min(data.page * data.pageSize, data.totalItems)} / {data.totalItems}</span><div>
      <button type="button" className="button button-secondary" disabled={loading || busy || page <= 1} onClick={() => setPage(x => x - 1)}>Trước</button><span> {page} / {Math.max(1, data.totalPages)} </span>
      <button type="button" className="button button-secondary" disabled={loading || busy || page >= data.totalPages} onClick={() => setPage(x => x + 1)}>Sau</button></div></nav>}
  </section>;
}
