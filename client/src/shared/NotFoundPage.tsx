import { Link } from 'react-router';

/** UI-12: trang không tồn tại. */
export function NotFoundPage() {
  return <main className="session-screen">
    <div className="empty-state">
      <div className="empty-icon" aria-hidden="true">?</div>
      <h1>Không tìm thấy trang</h1>
      <p>Đường dẫn không tồn tại hoặc bạn không có quyền xem trang này.</p>
      <Link className="button button-primary" to="/">Về trang chủ</Link>
    </div>
  </main>;
}
