import { Modal } from '../../shared/Modal';
import { LoadingState, LoadError } from '../../shared/PageState';
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
  const [searchError, setSearchError] = useState<string | null>(null);
  const [searchRevision, setSearchRevision] = useState(0);
  const [searchComplete, setSearchComplete] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    setSearchError(null);
    setSearchComplete(false);
    setSearching(false);
    setDirectoryUsers([]);
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
          setSearchComplete(true);
        }
      } catch {
        if (active) setSearchError('Không tải được danh bạ. Bạn có thể thử lại hoặc nhập tên đăng nhập trực tiếp.');
      } finally {
        if (active) setSearching(false);
      }
    }, 300);

    return () => {
      active = false;
      clearTimeout(timer);
    };
  }, [searchTerm, searchRevision]);

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
    <Modal title="Mời thành viên vào dự án" onClose={onClose} busy={submitting} className="feature-page">
        <p className="muted" style={{ marginBottom: 16 }}>
          Dự án: <strong>{projectName}</strong> (Hiện có {currentMemberCount}/5 thành viên).
        </p>

        <div className="notice" style={{ marginBottom: 20, fontSize: 13 }}>
          💡 <strong>Quy định BR-07:</strong> Mỗi dự án đồ án có tối đa <strong>5 thành viên</strong> (không tính Giảng viên hướng dẫn). Lời mời sẽ có hiệu lực trong vòng 7 ngày (BR-09).
        </div>

        {error && <div id="invite-error" role="alert" className="form-error" style={{ marginBottom: 16 }}>{error}</div>}

        <form onSubmit={handleSubmit} aria-busy={submitting}><fieldset disabled={submitting}>
          <div style={{ marginBottom: 16 }}>
            <label className="field-label" htmlFor="member-search">
              Tìm sinh viên trong danh bạ
            </label>
            <input
              id="member-search" data-autofocus
              type="text"
              placeholder="Nhập username hoặc tên sinh viên..."
              value={searchTerm}
              onChange={e => {
                setSearchTerm(e.target.value);
                setSelectedUsername(e.target.value);
              }}
              disabled={submitting}
            />
            {searching && <LoadingState label="Đang tìm sinh viên…" />}
            {searchError && <LoadError message={searchError} onRetry={() => setSearchRevision(value => value + 1)} />}
            {searchComplete && !searching && !searchError && directoryUsers.length === 0 && <p role="status">Không tìm thấy sinh viên. Bạn có thể nhập tên đăng nhập bên dưới.</p>}

            {directoryUsers.length > 0 && (
              <div
                style={{
                  marginTop: 8,
                  border: '1px solid var(--color-border-strong)',
                  borderRadius: 8,
                  background: 'var(--color-surface)',
                  maxHeight: 180,
                  overflowY: 'auto',
                }}
              >
                {directoryUsers.map(u => (
                  <button type="button" className="directory-option" disabled={u.isLocked || submitting}
                    key={u.userId}
                    onClick={() => {
                      setSelectedUsername(u.username);
                      setSearchTerm(u.username);
                      setDirectoryUsers([]);
                    }}
                    style={{
                      padding: '10px 14px',
                      cursor: u.isLocked ? 'not-allowed' : 'pointer',
                      borderBottom: '1px solid var(--color-border-soft)',
                      display: 'flex',
                      justifyContent: 'space-between',
                      alignItems: 'center',
                      background: selectedUsername === u.username ? 'var(--color-primary-tint)' : 'transparent',
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
                  </button>
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
              aria-invalid={!!error}
              aria-describedby={error ? 'invite-error' : undefined}
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
        </fieldset></form>
    </Modal>
  );
}

