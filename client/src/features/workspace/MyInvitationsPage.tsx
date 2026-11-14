import { useToast } from '../../shared/Toast';
import { LoadingState, LoadError, requestErrorMessage } from '../../shared/PageState';
import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router';
import { ApiError } from '../../shared/api';
import { workspaceApi } from './workspaceApi';
import type { InvitationItem } from './types';

export function MyInvitationsPage() {
  const notify = useToast();
  const [loadError, setLoadError] = useState<string | null>(null);
  const [invitations, setInvitations] = useState<InvitationItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [actionId, setActionId] = useState<string | null>(null);
  const [message, setMessage] = useState<{ text: string; type: 'success' | 'error' } | null>(null);

  const fetchInvitations = useCallback(async () => {
    setLoading(true);
    setMessage(null);
    setLoadError(null);
    try {
      const data = await workspaceApi.getMyInvitations();
      setInvitations(data);
    } catch (err: unknown) {
      setLoadError(requestErrorMessage(err, 'Không tải được danh sách lời mời.'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchInvitations();
  }, [fetchInvitations]);

  async function handleAccept(invitation: InvitationItem) {
    setActionId(invitation.id);
    setMessage(null);
    try {
      await workspaceApi.acceptInvitation(invitation.id);
      notify(`Đã chấp nhận lời mời tham gia dự án "${invitation.projectName}" thành công!`);
      await fetchInvitations();
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setMessage({ text: err.message, type: 'error' });
      } else {
        setMessage({ text: 'Thao tác chấp nhận lời mời thất bại.', type: 'error' });
      }
    } finally {
      setActionId(null);
    }
  }

  async function handleDecline(invitation: InvitationItem) {
    if (!window.confirm(`Bạn có chắc muốn từ chối lời mời vào dự án "${invitation.projectName}"?`)) {
      return;
    }

    setActionId(invitation.id);
    setMessage(null);
    try {
      await workspaceApi.declineInvitation(invitation.id);
      notify(`Đã từ chối lời mời vào dự án "${invitation.projectName}".`);
      await fetchInvitations();
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setMessage({ text: err.message, type: 'error' });
      } else {
        setMessage({ text: 'Thao tác từ chối lời mời thất bại.', type: 'error' });
      }
    } finally {
      setActionId(null);
    }
  }

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Accepted':
        return <span className="status-badge status-rendered">✓ Đã chấp nhận</span>;
      case 'Declined':
        return <span className="status-badge status-failed">✕ Đã từ chối</span>;
      case 'Expired':
        return <span className="status-badge status-failed">⏱ Đã hết hạn (BR-09)</span>;
      default:
        return <span className="status-badge status-pending">⏳ Chờ phản hồi</span>;
    }
  };

  return (
    <div className="feature-page">
      <div className="panel-header">
        <div>
          <h1>Lời mời tham gia dự án</h1>
          <p className="page-description">
            Danh sách các dự án đồ án capstone bạn được mời tham gia. Lời mời tự động hết hạn sau 7 ngày (BR-09).
          </p>
        </div>
      </div>

      {message && (
        <div role="alert" className="notice danger" style={{ marginBottom: 20 }}>
          {message.text}
          <div><button type="button" className="button button-secondary" onClick={fetchInvitations}>Thử lại</button></div>
        </div>
      )}

      <div className="panel">
        <div className="panel-header">
          <h2>Lời mời của bạn ({invitations.length})</h2>
        </div>

        {loading ? (
          <LoadingState label="Đang tải danh sách lời mời…" />
        ) : loadError ? (
          <LoadError message={loadError} onRetry={fetchInvitations} />
        ) : invitations.length === 0 ? (
          <div className="empty-state">
            <div className="empty-icon">✉️</div>
            <h2>Chưa có lời mời nào</h2>
            <Link to="/projects" className="button button-secondary">Về danh sách dự án</Link>
            <p className="muted">Khi trưởng nhóm (Leader) gửi lời mời tham gia dự án, bạn sẽ thấy xuất hiện tại đây.</p>
          </div>
        ) : (
          <div className="table-wrap" role="region" aria-label="Danh sách lời mời" tabIndex={0}>
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Tên dự án</th>
                  <th scope="col">Trạng thái</th>
                  <th scope="col">Thời gian gửi</th>
                  <th scope="col">Hạn phản hồi</th>
                  <th scope="col" style={{ textAlign: 'right' }}>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {invitations.map(inv => {
                  const isPending = inv.status === 'Pending';
                  return (
                    <tr key={inv.id}>
                      <td>
                        <strong>{inv.projectName}</strong>
                      </td>
                      <td>{getStatusBadge(inv.status)}</td>
                      <td>{new Date(inv.createdAt).toLocaleString('vi-VN')}</td>
                      <td>{new Date(inv.expiresAt).toLocaleString('vi-VN')}</td>
                      <td className="cell-actions">
                        {isPending ? (
                          <div style={{ display: 'inline-flex', gap: 8 }}>
                            <button
                              type="button"
                              className="button button-primary button-small"
                              onClick={() => handleAccept(inv)}
                              disabled={actionId === inv.id}
                            >
                              Chấp nhận
                            </button>
                            <button
                              type="button"
                              className="button button-secondary button-small"
                              onClick={() => handleDecline(inv)}
                              disabled={actionId === inv.id}
                            >
                              Từ chối
                            </button>
                          </div>
                        ) : (
                          <span style={{ fontSize: 13, color: 'var(--color-muted)' }}>Đã hoàn tất</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}

