import { useToast } from '../../shared/Toast';
import { LoadingState, LoadError, requestErrorMessage } from '../../shared/PageState';
import { Modal } from '../../shared/Modal';
import { useState, useEffect, useCallback, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router';
import { ApiError } from '../../shared/api';
import { workspaceApi } from './workspaceApi';
import type { ProjectRole, ProjectSummary } from './types';

export function ProjectsPage() {
  const notify = useToast();
  const [loadError, setLoadError] = useState<string | null>(null);
  const navigate = useNavigate();

  const [projects, setProjects] = useState<ProjectSummary[]>([]);
  const [totalItems, setTotalItems] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [page, setPage] = useState(1);
  const pageSize = 9;

  const [search, setSearch] = useState('');
  const [roleFilter, setRoleFilter] = useState<ProjectRole | ''>('');
  const [loading, setLoading] = useState(true);

  // Create Project Modal
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [newProjectName, setNewProjectName] = useState('');
  const [newProjectDescription, setNewProjectDescription] = useState('');
  const [creating, setCreating] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);

  const fetchProjects = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      const res = await workspaceApi.getProjects({
        search: search.trim() || undefined,
        role: roleFilter || undefined,
        page,
        pageSize,
        sort: 'createdAt:desc',
      });
      setProjects(res.items);
      setTotalItems(res.totalItems);
      setTotalPages(res.totalPages);
    } catch (err: unknown) {
      setLoadError(requestErrorMessage(err, 'Không tải được danh sách dự án.'));
    } finally {
      setLoading(false);
    }
  }, [search, roleFilter, page]);

  useEffect(() => {
    fetchProjects();
  }, [fetchProjects]);

  async function handleCreateProject(e: FormEvent) {
    e.preventDefault();
    if (!newProjectName.trim()) {
      setCreateError('Vui lòng nhập tên dự án.');
      return;
    }

    setCreating(true);
    setCreateError(null);
    try {
      const created = await workspaceApi.createProject({
        name: newProjectName.trim(),
        description: newProjectDescription.trim() || undefined,
      });
      setShowCreateModal(false);
      setNewProjectName('');
      setNewProjectDescription('');
      notify('Đã tạo dự án.');
      navigate(`/projects/${created.id}`);
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setCreateError(err.message);
      } else {
        setCreateError('Không thể tạo dự án. Vui lòng thử lại sau.');
      }
    } finally {
      setCreating(false);
    }
  }

  const getRoleBadge = (role: ProjectRole) => {
    switch (role) {
      case 'Leader':
        return <span className="status-badge status-rendered">👑 Trưởng nhóm</span>;
      case 'Lecturer':
        return <span className="status-badge status-pending">🎓 Giảng viên</span>;
      default:
        return <span className="status-badge">👤 Thành viên</span>;
    }
  };

  return (
    <div className="feature-page">
      <div className="panel-header" style={{ marginBottom: 24 }}>
        <div>
          <h1>Không gian Dự án UMLForge</h1>
          <p className="page-description">
            Quản lý các dự án capstone, cộng tác thiết kế sơ đồ UML và nộp duyệt đồ án với Giảng viên.
          </p>
        </div>

        <div style={{ display: 'flex', gap: 12 }}>
          <Link to="/invitations/mine" className="button button-secondary">
            ✉️ Lời mời của tôi
          </Link>
          <button
            type="button"
            className="button button-primary"
            onClick={() => setShowCreateModal(true)}
          >
            + Tạo dự án mới
          </button>
        </div>
      </div>

      {/* Filters Bar */}
      <div className="filters-bar">
        <div className="filter-item filter-item-search">
          <label htmlFor="search-proj">Tìm dự án ({totalItems})</label>
          <input
            id="search-proj"
            type="text"
            placeholder="Tìm theo tên dự án..."
            value={search}
            onChange={e => {
              setSearch(e.target.value);
              setPage(1);
            }}
          />
        </div>

        <div className="filter-item">
          <label htmlFor="filter-role">Vai trò của bạn</label>
          <select
            id="filter-role"
            value={roleFilter}
            onChange={e => {
              setRoleFilter(e.target.value as ProjectRole | '');
              setPage(1);
            }}
          >
            <option value="">Tất cả vai trò</option>
            <option value="Leader">Trưởng nhóm (Leader)</option>
            <option value="Member">Thành viên (Member)</option>
            <option value="Lecturer">Giảng viên hướng dẫn</option>
          </select>
        </div>
      </div>

      {/* Projects List Grid */}
      {loading ? (
        <LoadingState label="Đang tải danh sách dự án…" />
      ) : loadError ? (
        <LoadError message={loadError} onRetry={fetchProjects} />
      ) : projects.length === 0 ? (
        <div className="empty-state">
          <div className="empty-icon">📁</div>
          <h2>Chưa có dự án nào</h2>
          <p className="muted">
            Bạn chưa tham gia dự án nào. Hãy tạo dự án mới (bạn sẽ là Trưởng nhóm theo luật BR-06) hoặc kiểm tra Lời mời được gửi tới bạn!
          </p>
          <button
            type="button"
            className="button button-primary"
            onClick={() => setShowCreateModal(true)}
          >
            + Tạo dự án đầu tiên
          </button>
        </div>
      ) : (
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fill, minmax(min(320px, 100%), 1fr))',
            gap: 20,
            marginBottom: 32,
          }}
        >
          {projects.map(proj => (
            <Link
              key={proj.id}
              to={`/projects/${proj.id}`}
              className="card"
              style={{
                textDecoration: 'none',
                color: 'inherit',
                background: 'var(--color-surface)',
                border: '1px solid var(--color-border)',
                borderRadius: 'var(--radius-md)',
                padding: 24,
                display: 'flex',
                flexDirection: 'column',
                justifyContent: 'space-between',
                transition: 'transform 0.15s, box-shadow 0.15s',
              }}
            >
              <div>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
                  {getRoleBadge(proj.currentUserRole)}
                  <span style={{ fontSize: 12, color: 'var(--color-muted)' }}>
                    👥 {proj.memberCount}/5 thành viên
                  </span>
                </div>

                <h3 style={{ margin: '0 0 8px 0', color: 'var(--color-text)' }}>{proj.name}</h3>
                <p style={{ fontSize: 13, color: 'var(--color-muted)', margin: 0, lineHeight: 1.5, minHeight: 40 }}>
                  {proj.description || 'Chưa có mô tả cho dự án này.'}
                </p>
              </div>

              <div
                style={{
                  marginTop: 20,
                  paddingTop: 12,
                  borderTop: '1px solid var(--color-border-soft)',
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  fontSize: 12,
                  color: 'var(--color-muted)',
                }}
              >
                <span>Tạo ngày: {new Date(proj.createdAt).toLocaleDateString('vi-VN')}</span>
                <span style={{ color: 'var(--color-primary)', fontWeight: 600 }}>Chi tiết &rarr;</span>
              </div>
            </Link>
          ))}
        </div>
      )}

      {/* Pagination */}
      {totalPages > 1 && (
        <div style={{ display: 'flex', justifyContent: 'center', gap: 8, marginTop: 16 }}>
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

      {/* Create Project Modal */}
      {showCreateModal && (
        <Modal title="Tạo dự án Capstone mới" onClose={() => setShowCreateModal(false)} busy={creating} className="feature-page">
            <p className="muted" style={{ marginBottom: 16 }}>
              Theo quy tắc nghiệp vụ <strong>BR-06</strong>, người khởi tạo dự án sẽ tự động được cấp quyền <strong>Trưởng nhóm (Leader)</strong>.
            </p>

            {createError && <div id="create-project-error" role="alert" className="form-error" style={{ marginBottom: 16 }}>{createError}</div>}

            <form onSubmit={handleCreateProject} aria-busy={creating}><fieldset disabled={creating}>
              <div style={{ marginBottom: 16 }}>
                <label className="field-label" htmlFor="new-proj-name">
                  Tên dự án <span style={{ color: 'var(--color-danger)' }}>*</span>
                </label>
                <input
                  id="new-proj-name"
                  data-autofocus
                  aria-invalid={!!createError}
                  aria-describedby={createError ? 'create-project-error' : undefined}
                  type="text"
                  placeholder="Ví dụ: Đồ án Hệ thống Quản lý Bệnh viện..."
                  value={newProjectName}
                  onChange={e => setNewProjectName(e.target.value)}
                  disabled={creating}
                  required
                />
              </div>

              <div style={{ marginBottom: 20 }}>
                <label className="field-label" htmlFor="new-proj-desc">
                  Mô tả dự án
                </label>
                <textarea
                  id="new-proj-desc"
                  rows={3}
                  className="code-textarea"
                  style={{ height: 'auto', minHeight: 80 }}
                  placeholder="Mô tả phạm vi hoặc đề tài đồ án..."
                  value={newProjectDescription}
                  onChange={e => setNewProjectDescription(e.target.value)}
                  disabled={creating}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
                <button
                  type="button"
                  className="button button-secondary"
                  onClick={() => setShowCreateModal(false)}
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  className="button button-primary"
                  disabled={creating || !newProjectName.trim()}
                >
                  {creating ? 'Đang tạo...' : 'Tạo dự án'}
                </button>
              </div>
            </fieldset></form>
        </Modal>
      )}
    </div>
  );
}
