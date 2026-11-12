import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { useAuth } from '../../auth/AuthContext';
import { listNotifications, markRead, notificationLink, notificationTime, notifyRead, onRead, unreadCount } from './notificationApi';
import type { NotificationItem } from './notificationApi';
import './notification.css';

export function NotificationBell() {
  const { user } = useAuth();
  return user ? <Bell key={user.id} userId={user.id} /> : null;
}
function Bell({ userId }: { userId: string }) {
  const [count, setCount] = useState<number | null>(null);
  const [countError, setCountError] = useState(false);
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState<NotificationItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [revision, setRevision] = useState(0);
  const root = useRef<HTMLDivElement>(null);
  const trigger = useRef<HTMLButtonElement>(null);
  const mutation = useRef<AbortController | null>(null);
  const navigate = useNavigate();
  useEffect(() => () => mutation.current?.abort(), []);
  useEffect(() => {
    let stopped = false;
    let timer: ReturnType<typeof setTimeout> | undefined;
    let controller = new AbortController();
    async function poll() {
      controller.abort(); controller = new AbortController();
      try { const data = await unreadCount(controller.signal); if (!stopped) { setCount(data.count); setCountError(false); } }
      catch { if (!stopped && !controller.signal.aborted) setCountError(true); }
      finally { if (!stopped) timer = setTimeout(poll, 30_000); }
    }
    const unsubscribe = onRead(userId, () => { clearTimeout(timer); controller.abort(); setRevision(x => x + 1); });
    void poll();
    return () => { stopped = true; clearTimeout(timer); controller.abort(); unsubscribe(); };
  }, [userId, revision]);
  useEffect(() => {
    if (!open) return;
    const controller = new AbortController();
    setLoading(true); setError('');
    listNotifications(1, false, controller.signal, 10).then(data => { if (!controller.signal.aborted) setItems(data.items); })
      .catch((e: unknown) => { if (!controller.signal.aborted) setError(e instanceof Error ? e.message : 'Không tải được thông báo.'); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [open, revision]);
  useEffect(() => {
    if (!open) return;
    const outside = (event: PointerEvent) => { if (!root.current?.contains(event.target as Node)) setOpen(false); };
    const escape = (event: KeyboardEvent) => { if (event.key === 'Escape') { setOpen(false); trigger.current?.focus(); } };
    document.addEventListener('pointerdown', outside); document.addEventListener('keydown', escape);
    return () => { document.removeEventListener('pointerdown', outside); document.removeEventListener('keydown', escape); };
  }, [open]);
  async function visit(item: NotificationItem) {
    if (busy) return;
    const controller = new AbortController(); mutation.current = controller; setBusy(true); setError('');
    try {
      if (!item.isRead) { await markRead(item.id, controller.signal); if (controller.signal.aborted) return; notifyRead(userId); }
      setOpen(false); navigate(notificationLink(item.link));
    } catch (e) { if (!controller.signal.aborted) setError(e instanceof Error ? e.message : 'Không đánh dấu được thông báo.'); }
    finally { if (!controller.signal.aborted) setBusy(false); }
  }
  return <div className="notif-bell" ref={root}>
    <button className="button button-secondary" type="button" ref={trigger} aria-expanded={open} aria-controls="notification-dropdown"
      aria-label={`Thông báo${count === null ? '' : `, ${count} chưa đọc`}${countError ? ', chưa cập nhật được' : ''}`} onClick={() => setOpen(x => !x)}>
      <span aria-hidden="true">🔔</span><span className="notif-bell-label">Thông báo</span>
      {count !== null && count > 0 && <span className="notif-count">{count > 99 ? '99+' : count}</span>}
      {countError && <span title="Chưa cập nhật được số chưa đọc">!</span>}
    </button>
    {open && <section id="notification-dropdown" className="notif-dropdown" aria-label="Thông báo mới nhất">
      <div className="notif-dropdown-heading"><strong>Thông báo mới nhất</strong><button type="button" className="notif-text-button" onClick={() => setRevision(x => x + 1)}>Làm mới</button></div>
      {loading ? <div className="notif-skeleton" role="status">Đang tải thông báo…</div> : error ? <div className="notif-error" role="alert">{error}<button type="button" onClick={() => setRevision(x => x + 1)}>Thử lại</button></div> : items.length === 0 ? <p className="notif-empty">Bạn chưa có thông báo.</p> : <ul className="notif-list">
        {items.map(item => <li key={item.id}><button type="button" disabled={busy} className={item.isRead ? 'notif-entry' : 'notif-entry notif-unread'} onClick={() => void visit(item)}>
          <span>{!item.isRead && <span className="notif-dot" aria-label="Chưa đọc">● </span>}{item.title}</span><time dateTime={item.createdAt}>{notificationTime(item.createdAt)}</time>
        </button></li>)}
      </ul>}
      <Link to="/notifications" className="notif-all" onClick={() => setOpen(false)}>Xem tất cả thông báo →</Link>
    </section>}
  </div>;
}
