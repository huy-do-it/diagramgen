import { useState, useEffect, type FormEvent } from 'react';
import { ApiError } from '../../shared/api';
import { workspaceApi } from './workspaceApi';
import type { UserDirectoryItem } from './types';

interface InviteMemberModalProps {
  projectId: string;
  projectName: string;
  currentMemberCount: number;
  onClose: () => void;
  onSuccess: () => void;
}

export function InviteMemberModal({
  projectId,
  projectName,
  currentMemberCount,
  onClose,
  onSuccess,
}: InviteMemberModalProps) {
  const [searchTerm, setSearchTerm] = useState('');
  const [directoryUsers, setDirectoryUsers] = useState<UserDirectoryItem[]>([]);
  const [selectedUsername, setSelectedUsername] = useState('');
  const [searching, setSearching] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    if (!searchTerm.trim()) {
      setDirectoryUsers([]);
      return;
    }

    const timer = setTimeout(async () => {
      setSearching(true);
      try {
        const res = await workspaceApi.getDirectoryUsers({
          search: searchTerm.trim(),
          role: 'Student',
          pageSize: 5,
        });
        if (active) {
          setDirectoryUsers(res.items);
        }
      } catch {
        // bỏ qua lỗi live search
      } finally {
        if (active) setSearching(false);
      }
    }, 300);

    return () => {
      active = false;
      clearTimeout(timer);
    };
  }, [searchTerm]);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!selectedUsername.trim()) {
      setError('Vui lòng chọn hoặc nhập tên đăng nhập của thành viên cần mời.');
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      await workspaceApi.inviteMember(projectId, selectedUsername.trim());
      onSuccess();
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError('Không gửi được lời mời. Vui lòng thử lại sau.');
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal" style={{ maxWidth: 500 }} onClick={e => e.stopPropagation()}>
        <div className="modal-header">
          <h2>Mời thành viên vào dự án</h2>
          <button type="button" className="button button-secondary button-small" onClick={onClose}>
            ✕
          </button>
        </div>

        <p className="muted" style={{ marginBottom: 16 }}>
          Dự án: <strong>{projectName}</strong> (Hiện có {currentMemberCount}/5 thành viên).
        </p>

        <div className="notice" style={{ marginBottom: 20, fontSize: 13 }}>
          💡 <strong>Quy định BR-07:</strong> Mỗi dự án đồ án có tối đa <strong>5 thành viên</strong> (không tính Giảng viên hướng dẫn). Lời mời sẽ có hiệu lực trong vòng 7 ngày (BR-09).
        </div>

        {error && <div className="form-error" style={{ marginBottom: 16 }}>{error}</div>}

        <form onSubmit={handleSubmit}>
          <div style={{ marginBottom: 16 }}>
            <label className="field-label" htmlFor="member-search">
              Tìm sinh viên từ Danh bạ nhân bản
            </label>
            <input
              id="member-search"
              type="text"
              placeholder="Nhập username hoặc tên sinh viên..."
              value={searchTerm}
              onChange={e => {
                setSearchTerm(e.target.value);
                setSelectedUsername(e.target.value);
              }}
              disabled={submitting}
            />
            {searching && <span style={{ fontSize: 12, color: '#687389', marginTop: 4, display: 'block' }}>Đang tìm kiếm...</span>}

            {directoryUsers.length > 0 && (
              <div
                style={{
                  marginTop: 8,
                  border: '1px solid #d9dfea',
                  borderRadius: 8,
                  background: '#fff',
                  maxHeight: 180,
                  overflowY: 'auto',
                }}
              >
                {directoryUsers.map(u => (
                  <div
                    key={u.userId}
                    onClick={() => {
                      setSelectedUsername(u.username);
                      setSearchTerm(u.username);
                      setDirectoryUsers([]);
                    }}
                    style={{
                      padding: '10px 14px',
                      cursor: u.isLocked ? 'not-allowed' : 'pointer',
                      borderBottom: '1px solid #f1f3f8',
                      display: 'flex',
                      justifyContent: 'space-between',
                      alignItems: 'center',
                      background: selectedUsername === u.username ? '#edf0ff' : 'transparent',
                    }}
                  >
                    <div>
                      <strong>{u.username}</strong> — {u.fullName}
                    </div>
                    {u.isLocked ? (
                      <span className="status-badge status-failed">Đã khóa</span>
                    ) : (
                      <span className="status-badge">{u.role}</span>
                    )}
                  </div>
                ))}
              </div>
            )}
          </div>

          <div style={{ marginBottom: 20 }}>
            <label className="field-label" htmlFor="selected-username">
              Tên đăng nhập được mời
            </label>
            <input
              id="selected-username"
              type="text"
              placeholder="Ví dụ: student2"
              value={selectedUsername}
              onChange={e => setSelectedUsername(e.target.value)}
              disabled={submitting}
              required
            />
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
            <button type="button" className="button button-secondary" onClick={onClose}>
              Hủy
            </button>
            <button
              type="submit"
              className="button button-primary"
              disabled={submitting || !selectedUsername.trim()}
            >
              {submitting ? 'Đang gửi...' : 'Gửi lời mời'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

