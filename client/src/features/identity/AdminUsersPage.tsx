import { useToast } from '../../shared/Toast';
import { LoadingState, LoadError, requestErrorMessage } from '../../shared/PageState';
import { Modal } from '../../shared/Modal';
import { useEffect, useState, useCallback, type FormEvent } from 'react';
import { useAuth } from '../../auth/AuthContext';
import { ApiError } from '../../shared/api';
import { identityApi } from './identityApi';
import type { UserItem, UserRole } from './types';

export function AdminUsersPage() {
  const notify = useToast();
  const [loadError, setLoadError] = useState<string | null>(null);
  const { user: currentUser } = useAuth();

  const [users, setUsers] = useState<UserItem[]>([]);
  const [totalItems, setTotalItems] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [page, setPage] = useState(1);
  const pageSize = 10;

  // Filters
  const [searchTerm, setSearchTerm] = useState('');
  const [roleFilter, setRoleFilter] = useState<UserRole | ''>('');
  const [lockedFilter, setLockedFilter] = useState<'all' | 'active' | 'locked'>('all');

  const [loading, setLoading] = useState(true);
  const [actionLoadingId, setActionLoadingId] = useState<string | null>(null);
  const [message, setMessage] = useState<{ text: string; type: 'success' | 'error' } | null>(null);

  // Edit role modal state
  const [selectedUserForRole, setSelectedUserForRole] = useState<UserItem | null>(null);
  const [newRole, setNewRole] = useState<UserRole>('Student');

  const fetchUsers = useCallback(async () => {
    setLoading(true);
    setMessage(null);
    setLoadError(null);
    try {
      const isLockedParam = lockedFilter === 'all' ? undefined : lockedFilter === 'locked';
      const res = await identityApi.getUsers({
        search: searchTerm.trim() || undefined,
        role: roleFilter || undefined,
        isLocked: isLockedParam,
        page,
        pageSize,
        sort: 'createdAt:desc',
      });
      setUsers(res.items);
      setTotalItems(res.totalItems);
      setTotalPages(res.totalPages);
    } catch (err: unknown) {
      setLoadError(requestErrorMessage(err, 'Không tải được danh sách người dùng.'));
    } finally {
      setLoading(false);
    }
  }, [searchTerm, roleFilter, lockedFilter, page]);

  useEffect(() => {
    fetchUsers();
  }, [fetchUsers]);

  async function handleToggleLock(targetUser: UserItem) {
    if (targetUser.id === currentUser?.id) {
      setMessage({ text: 'Quy chuẩn BR-04: Quản trị viên không được phép tự khóa tài khoản của chính mình.', type: 'error' });
      return;
    }

    const nextLockState = !targetUser.isLocked;
    const confirmText = nextLockState
      ? `Bạn có chắc chắn muốn khóa tài khoản "${targetUser.username}"? Người dùng này sẽ không thể đăng nhập.`
      : `Bạn có chắc chắn muốn mở khóa cho tài khoản "${targetUser.username}"?`;

    if (!window.confirm(confirmText)) return;

    setActionLoadingId(targetUser.id);
    setMessage(null);
    try {
      await identityApi.lockUser(targetUser.id, nextLockState);
      notify(`Đã ${nextLockState ? 'khóa' : 'mở khóa'} thành công tài khoản "${targetUser.username}".`);
      await fetchUsers();
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setMessage({ text: err.message, type: 'error' });
      } else {
        setMessage({ text: 'Thao tác khóa/mở khóa thất bại.', type: 'error' });
      }
    } finally {
      setActionLoadingId(null);
    }
  }

  async function handleSaveRole(e: FormEvent) {
    e.preventDefault();
    if (!selectedUserForRole) return;

    setActionLoadingId(selectedUserForRole.id);
    setMessage(null);
    try {
      await identityApi.updateRole(selectedUserForRole.id, newRole);
      notify(`Đã cập nhật vai trò của "${selectedUserForRole.username}" thành ${newRole}.`);
      setSelectedUserForRole(null);
      await fetchUsers();
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setMessage({ text: err.message, type: 'error' });
      } else {
        setMessage({ text: 'Cập nhật vai trò thất bại.', type: 'error' });
      }
    } finally {
      setActionLoadingId(null);
    }
  }

  if (currentUser?.role !== 'Admin') {
    return (
      <div className="panel" style={{ textAlign: 'center', padding: '60px 20px' }}>
        <h2>Không có quyền truy cập</h2>
        <p className="muted">Trang quản trị người dùng chỉ dành riêng cho tài khoản có vai trò Quản trị viên (Admin).</p>
      </div>
    );
  }

  return (
    <div className="feature-page">
      <div className="panel-header">
        <div>
          <h1>Quản lý người dùng hệ thống</h1>
          <p className="page-description">
            Xem danh sách, tìm kiếm, phân quyền (BR-04) và khóa/mở khóa tài khoản (BR-03).
          </p>
        </div>
      </div>

      {message && (
        <div
          role="alert" className="notice danger"
          style={{ marginBottom: 20 }}
        >
          {message.text}
          <div><button type="button" className="button button-secondary" onClick={fetchUsers}>Thử lại</button></div>
        </div>
      )}

      {/* Filters Bar */}
      <div className="filters-bar">
        <div className="filter-item filter-item-search">
          <label htmlFor="search-user">Tìm kiếm</label>
          <input
            id="search-user"
            type="text"
            placeholder="Tìm theo username hoặc họ tên..."
            value={searchTerm}
            onChange={e => {
              setSearchTerm(e.target.value);
              setPage(1);
            }}
          />
        </div>

        <div className="filter-item">
          <label htmlFor="filter-role">Vai trò</label>
          <select
            id="filter-role"
            value={roleFilter}
            onChange={e => {
              setRoleFilter(e.target.value as UserRole | '');
              setPage(1);
            }}
          >
            <option value="">Tất cả vai trò</option>
            <option value="Student">Sinh viên (Student)</option>
            <option value="Lecturer">Giảng viên (Lecturer)</option>
            <option value="Admin">Quản trị viên (Admin)</option>
          </select>
        </div>

        <div className="filter-item">
          <label htmlFor="filter-status">Trạng thái</label>
          <select
            id="filter-status"
            value={lockedFilter}
            onChange={e => {
              setLockedFilter(e.target.value as 'all' | 'active' | 'locked');
              setPage(1);
            }}
          >
            <option value="all">Tất cả trạng thái</option>
            <option value="active">Đang hoạt động</option>
            <option value="locked">Đã bị khóa</option>
          </select>
        </div>
      </div>

      {/* Table */}
      <div className="panel">
        <div className="panel-header">
          <h2>Danh sách người dùng ({totalItems})</h2>
        </div>

        {loading ? (
          <LoadingState label="Đang tải danh sách người dùng…" />
        ) : loadError ? (
          <LoadError message={loadError} onRetry={fetchUsers} />
        ) : users.length === 0 ? (
          <div className="empty-state">
            <div className="empty-icon">👥</div>
            <h2>Không tìm thấy người dùng</h2>
            <p className="muted">Không có người dùng nào khớp với tiêu chí tìm kiếm hiện tại.</p>
          </div>
        ) : (
          <div className="table-wrap" role="region" aria-label="Danh sách người dùng" tabIndex={0}>
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Tên đăng nhập</th>
                  <th scope="col">Họ và tên</th>
                  <th scope="col">Vai trò</th>
                  <th scope="col">Trạng thái</th>
                  <th scope="col">Ngày tạo</th>
                  <th scope="col" style={{ textAlign: 'right' }}>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {users.map(u => (
                  <tr key={u.id}>
                    <td>
                      <strong>{u.username}</strong>
                      {u.id === currentUser?.id && (
                        <span className="status-badge" style={{ marginLeft: 8, background: 'var(--color-info-bg)', color: 'var(--color-info)' }}>
                          (Bạn)
                        </span>
                      )}
                    </td>
                    <td>{u.fullName}</td>
                    <td>
                      <span className={`status-badge ${u.role === 'Admin' ? 'status-failed' : u.role === 'Lecturer' ? 'status-pending' : ''}`}>
                        {u.role}
                      </span>
                    </td>
                    <td>
                      {u.isLocked ? (
                        <span className="status-badge status-failed">Đã khóa</span>
                      ) : (
                        <span className="status-badge status-rendered">Hoạt động</span>
                      )}
                    </td>
                    <td>{new Date(u.createdAt).toLocaleDateString('vi-VN')}</td>
                    <td className="cell-actions">
                      <button
                        type="button"
                        className="button button-secondary button-small"
                        onClick={() => {
                          setMessage(null);
                          setSelectedUserForRole(u);
                          setNewRole(u.role);
                        }}
                        disabled={actionLoadingId === u.id}
                        aria-label={`Đổi vai trò cho ${u.username}`}
                        style={{ marginRight: 8 }}
                      >
                        Đổi vai trò
                      </button>

                      <button
                        type="button"
                        className={`button button-small ${u.isLocked ? 'button-secondary' : 'button-danger'}`}
                        onClick={() => handleToggleLock(u)}
                        disabled={actionLoadingId === u.id || u.id === currentUser?.id}
                        aria-label={`${u.isLocked ? 'Mở khóa' : 'Khóa'} tài khoản ${u.username}`}
                      >
                        {u.isLocked ? 'Mở khóa' : 'Khóa'}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {/* Pagination */}
        {totalPages > 1 && (
          <div style={{ display: 'flex', justifyContent: 'center', gap: 8, marginTop: 24 }}>
            <button
              type="button"
              className="button button-secondary button-small"
              disabled={page <= 1 || loading}
              onClick={() => setPage(p => Math.max(1, p - 1))}
            >
              &larr; Trang trước
            </button>
            <span style={{ display: 'inline-flex', alignItems: 'center', padding: '0 12px', fontSize: 13, color: 'var(--color-muted)' }}>
              Trang {page} / {totalPages}
            </span>
            <button
              type="button"
              className="button button-secondary button-small"
              disabled={page >= totalPages || loading}
              onClick={() => setPage(p => Math.min(totalPages, p + 1))}
            >
              Trang sau &rarr;
            </button>
          </div>
        )}
      </div>

      {/* Modal Change Role */}
      {selectedUserForRole && (
        <Modal title="Cập nhật vai trò" onClose={() => setSelectedUserForRole(null)} busy={!!actionLoadingId} className="feature-page">
            <p className="muted" style={{ marginBottom: 16 }}>
              Chọn vai trò mới cho người dùng <strong>{selectedUserForRole.username}</strong> ({selectedUserForRole.fullName}):
            </p>

            {message && <div className="notice danger" role="alert">{message.text}</div>}
            <form onSubmit={handleSaveRole} aria-busy={!!actionLoadingId}><fieldset disabled={!!actionLoadingId}>
              <div style={{ marginBottom: 20 }}>
                <label className="field-label" htmlFor="select-new-role">Vai trò mới</label>
                <select
                  id="select-new-role" data-autofocus
                  className="select"
                  value={newRole}
                  onChange={e => setNewRole(e.target.value as UserRole)}
                  style={{ width: '100%', maxWidth: '100%' }}
                >
                  <option value="Student">Sinh viên (Student)</option>
                  <option value="Lecturer">Giảng viên (Lecturer)</option>
                  <option value="Admin">Quản trị viên (Admin)</option>
                </select>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
                <button
                  type="button"
                  className="button button-secondary"
                  onClick={() => setSelectedUserForRole(null)}
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  className="button button-primary"
                  disabled={actionLoadingId === selectedUserForRole.id}
                >
                  {actionLoadingId === selectedUserForRole.id ? 'Đang lưu...' : 'Lưu thay đổi'}
                </button>
              </div>
            </fieldset></form>
        </Modal>
      )}
    </div>
  );
}

