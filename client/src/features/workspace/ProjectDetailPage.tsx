import { useToast } from '../../shared/Toast';
import { LoadingState, LoadError, requestErrorMessage } from '../../shared/PageState';
import { Modal } from '../../shared/Modal';
import { useEffect, useState, useCallback, useRef, type FormEvent } from 'react';
import { useParams, useNavigate, useLocation, useSearchParams, Link } from 'react-router';
import { useAuth } from '../../auth/AuthContext';
import { ApiError } from '../../shared/api';
import { workspaceApi } from './workspaceApi';
import { InviteMemberModal } from './InviteMemberModal';
import { DiagramListPanel } from '../diagram/DiagramListPanel';
import { ReviewPanel } from '../diagram/ReviewPanel';
import { CreateDiagramModal } from '../diagram/CreateDiagramModal';
import type { ProjectDetail, ProjectMemberItem, UserDirectoryItem } from './types';

export function ProjectDetailPage() {
  const { id } = useParams<{ id: string }>();
  return <ProjectDetailContent key={id} />;
}

function ProjectDetailContent() {
  const notify = useToast();
  const mounted = useRef(false);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  const { id } = useParams<{ id: string }>();
  const { user: currentUser } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams, setSearchParams] = useSearchParams();

  const [project, setProject] = useState<ProjectDetail | null>(null);
  const [etag, setEtag] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [message, setMessage] = useState<{ text: string; type: 'success' | 'error' } | null>(null);

  // Active Tab
  const tab = searchParams.get('tab');
  const activeTab = tab === 'diagrams' ? 'diagrams' : tab === 'review' || tab === 'reviews' ? 'reviews' : 'members';
  function setActiveTab(value: 'members' | 'diagrams' | 'reviews') {
    setSearchParams(previous => {
      const next = new URLSearchParams(previous);
      if (value === 'members') next.delete('tab');
      else next.set('tab', value === 'reviews' ? 'review' : value);
      return next;
    });
  }
  const diagramNavigation = { state: { diagramListPath: location.pathname + location.search } };

  // Edit Project State
  const [editingInfo, setEditingInfo] = useState(false);
  const [editName, setEditName] = useState('');
  const [editDesc, setEditDesc] = useState('');

  // Invite Modal
  const [showInviteModal, setShowInviteModal] = useState(false);

  // Assign Lecturer Modal
  const [showLecturerModal, setShowLecturerModal] = useState(false);
  const [lecturers, setLecturers] = useState<UserDirectoryItem[]>([]);
  const [selectedLecturerId, setSelectedLecturerId] = useState('');
  const [loadingLecturers, setLoadingLecturers] = useState(false);

  // Create Diagram Modal State
  const [showCreateDiagramModal, setShowCreateDiagramModal] = useState(false);
  const fetchProject = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    setMessage(null);
    try {
      const data = await workspaceApi.getProject(id);
      if (!mounted.current) return;
      setProject(data.project);
      setEtag(data.etag);
      setEditName(data.project.name);
      setEditDesc(data.project.description || '');
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setMessage({ text: requestErrorMessage(err, 'Không tải được thông tin dự án.'), type: 'error' });
      } else {
        setMessage({ text: 'Không tải được thông tin dự án.', type: 'error' });
      }
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    fetchProject();
  }, [fetchProject]);

  const currentMember = project?.members.find(m => m.userId === currentUser?.id);
  const isLeader = currentMember?.role === 'Leader';
  const isLecturer = currentMember?.role === 'Lecturer';
  const isMember = currentMember?.role === 'Member';

  // Count non-lecturer members
  const studentMemberCount = project?.members.filter(m => m.role !== 'Lecturer').length || 0;

  async function handleUpdateInfo(e: FormEvent) {
    e.preventDefault();
    if (!id || !project) return;

    setActionLoading(true);
    setMessage(null);
    try {
      const updated = await workspaceApi.updateProject(id, {
        name: editName.trim(),
        description: editDesc.trim() || undefined,
      }, etag);

      setProject(updated);
      setEditingInfo(false);
      notify('Cập nhật thông tin dự án thành công!');
      await fetchProject();
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setMessage({ text: requestErrorMessage(err, 'Không tải được thông tin dự án.'), type: 'error' });
      } else {
        setMessage({ text: 'Không thể cập nhật thông tin dự án.', type: 'error' });
      }
    } finally {
      setActionLoading(false);
    }
  }

  async function handleDeleteProject() {
    if (!id || !project) return;
    const confirm = window.confirm(`CẢNH BÁO: Bạn có chắc chắn muốn xóa dự án "${project.name}" không? Thao tác này sẽ xóa mềm toàn bộ dự án.`);
    if (!confirm) return;

    setActionLoading(true);
    try {
      await workspaceApi.deleteProject(id);
      navigate('/projects', { replace: true });
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setMessage({ text: requestErrorMessage(err, 'Không tải được thông tin dự án.'), type: 'error' });
      } else {
        setMessage({ text: 'Xóa dự án thất bại.', type: 'error' });
      }
      setActionLoading(false);
    }
  }

  async function handleRemoveMember(member: ProjectMemberItem) {
    if (!id) return;
    if (member.role === 'Leader') {
      setMessage({ text: 'Quy tắc BR-08: Không thể xóa Trưởng nhóm (Leader) ra khỏi dự án.', type: 'error' });
      return;
    }

    if (!window.confirm(`Bạn có chắc muốn xóa thành viên "${member.fullName || member.username}" khỏi dự án?`)) {
      return;
    }

    setActionLoading(true);
    setMessage(null);
    try {
      await workspaceApi.removeMember(id, member.userId);
      notify(`Đã xóa thành viên "${member.username}" khỏi dự án.`);
      await fetchProject();
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setMessage({ text: requestErrorMessage(err, 'Không tải được thông tin dự án.'), type: 'error' });
      } else {
        setMessage({ text: 'Không thể xóa thành viên.', type: 'error' });
      }
    } finally {
      setActionLoading(false);
    }
  }

  async function openLecturerModal() {
    setMessage(null);
    setSelectedLecturerId('');
    setShowLecturerModal(true);
    setLoadingLecturers(true);
    try {
      const res = await workspaceApi.getDirectoryUsers({ role: 'Lecturer', pageSize: 50 });
      setLecturers(res.items);
      if (res.items.length > 0) {
        setSelectedLecturerId(res.items[0].userId);
      }
    } catch {
      setMessage({ text: 'Không tải được danh sách giảng viên từ danh bạ.', type: 'error' });
    } finally {
      setLoadingLecturers(false);
    }
  }

  async function handleAssignLecturer(e: FormEvent) {
    e.preventDefault();
    if (!id || !selectedLecturerId) return;

    setActionLoading(true);
    setMessage(null);
    try {
      await workspaceApi.assignLecturer(id, selectedLecturerId);
      setShowLecturerModal(false);
      notify('Gán Giảng viên hướng dẫn thành công (BR-10)!');
      await fetchProject();
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setMessage({ text: requestErrorMessage(err, 'Không tải được thông tin dự án.'), type: 'error' });
      } else {
        setMessage({ text: 'Gán giảng viên thất bại.', type: 'error' });
      }
    } finally {
      setActionLoading(false);
    }
  }

  if (loading) {
    return <div className="feature-page"><LoadingState label="Đang tải thông tin dự án…" /></div>;
  }

  if (!project) {
    return (
      <div className="feature-page panel" style={{ textAlign: 'center', padding: '60px 20px' }}>
        <h2>Không tìm thấy dự án</h2>
        {message && <LoadError message={message.text} onRetry={fetchProject} />}
        <p className="muted">Dự án này không tồn tại hoặc bạn không có quyền truy cập.</p>
        <Link to="/projects" className="button button-primary">
          &larr; Về danh sách dự án
        </Link>
      </div>
    );
  }

  return (
    <div className="feature-page">
      {/* Header Back & Actions */}
      <div style={{ marginBottom: 16 }}>
        <Link to="/projects" style={{ textDecoration: 'none', color: 'var(--color-muted)', fontSize: 13, fontWeight: 600 }}>
          &larr; Quay lại danh sách dự án
        </Link>
      </div>

      {message && (
        <div role="alert" className="notice danger" style={{ marginBottom: 20 }}>
          {message.text}
        </div>
      )}

      {/* Project Banner Card */}
      <div className="panel" style={{ marginBottom: 20 }}>
        {editingInfo ? (
          <form onSubmit={handleUpdateInfo} aria-busy={actionLoading}>
            <div style={{ marginBottom: 14 }}>
              <label className="field-label" htmlFor="edit-name">Tên dự án</label>
              <input
                id="edit-name"
                type="text"
                value={editName}
                onChange={e => setEditName(e.target.value)}
                required
                disabled={actionLoading}
              />
            </div>

            <div style={{ marginBottom: 16 }}>
              <label className="field-label" htmlFor="edit-desc">Mô tả</label>
              <textarea
                id="edit-desc"
                rows={3}
                className="code-textarea"
                style={{ height: 'auto', minHeight: 70 }}
                value={editDesc}
                onChange={e => setEditDesc(e.target.value)}
                disabled={actionLoading}
              />
            </div>

            <div style={{ display: 'flex', gap: 10 }}>
              <button
                type="button"
                className="button button-secondary button-small"
                onClick={() => setEditingInfo(false)}
              >
                Hủy
              </button>
              <button
                type="submit"
                className="button button-primary button-small"
                disabled={actionLoading}
              >
                {actionLoading ? 'Đang lưu...' : 'Lưu thông tin'}
              </button>
            </div>
          </form>
        ) : (
          <div>
            <div className="panel-header" style={{ marginBottom: 12 }}>
              <div>
                <span className="welcome-tag" style={{ marginBottom: 6 }}>DỰ ÁN CAPSTONE</span>
                <h1 style={{ margin: 0, color: 'var(--color-text)' }}>{project.name}</h1>
              </div>

              {isLeader && (
                <div style={{ display: 'flex', gap: 10 }}>
                  <button
                    type="button"
                    className="button button-secondary button-small"
                    onClick={() => setEditingInfo(true)}
                  >
                    ✏️ Sửa thông tin
                  </button>
                  <button
                    type="button"
                    className="button button-danger button-small"
                    onClick={handleDeleteProject}
                    disabled={actionLoading}
                  >
                    🗑️ Xóa dự án
                  </button>
                </div>
              )}
            </div>

            <p style={{ color: '#556079', fontSize: 14, margin: '0 0 16px 0', lineHeight: 1.6 }}>
              {project.description || 'Chưa có mô tả chi tiết cho dự án này.'}
            </p>

            <div style={{ display: 'flex', gap: 20, flexWrap: 'wrap', fontSize: 12, color: 'var(--color-muted)' }}>
              <span>📅 Ngày tạo: {new Date(project.createdAt).toLocaleDateString('vi-VN')}</span>
              <span>👥 Số thành viên: {studentMemberCount}/5</span>
            </div>
          </div>
        )}
      </div>

      {/* Tabs */}
      <div className="tabs">
        <button
          type="button"
          className={`tab-btn ${activeTab === 'members' ? 'active' : ''}`}
          onClick={() => setActiveTab('members')}
        >
          👥 Thành viên &amp; Giảng viên ({project.members.length})
        </button>
        <button
          type="button"
          className={`tab-btn ${activeTab === 'diagrams' ? 'active' : ''}`}
          onClick={() => setActiveTab('diagrams')}
        >
          📐 Sơ đồ UML
        </button>
        <button
          type="button"
          className={`tab-btn ${activeTab === 'reviews' ? 'active' : ''}`}
          onClick={() => setActiveTab('reviews')}
        >
          ⚖️ Thẩm định &amp; Đánh giá
        </button>
      </div>

      {/* Tab 1: Thành viên & Giảng viên */}
      {activeTab === 'members' && (
        <div className="panel">
          <div className="panel-header">
            <div>
              <h2>Danh sách Thành viên dự án</h2>
              <p className="muted" style={{ margin: 0, fontSize: 13 }}>
                Quy định BR-07: Tối đa 5 thành viên sinh viên. Giảng viên hướng dẫn được gán riêng (BR-10).
              </p>
            </div>

            {isLeader && (
              <div style={{ display: 'flex', gap: 10 }}>
                <button
                  type="button"
                  className="button button-secondary button-small"
                  onClick={openLecturerModal}
                >
                  🎓 Gán Giảng viên (BR-10)
                </button>
                <button
                  type="button"
                  className="button button-primary button-small"
                  onClick={() => setShowInviteModal(true)}
                  disabled={studentMemberCount >= 5}
                >
                  + Mời thành viên ({studentMemberCount}/5)
                </button>
              </div>
            )}
          </div>

          <div className="table-wrap" role="region" aria-label="Danh sách thành viên" tabIndex={0}>
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Tên người dùng</th>
                  <th scope="col">Họ và tên</th>
                  <th scope="col">Vai trò trong nhóm</th>
                  <th scope="col">Ngày tham gia</th>
                  {isLeader && <th scope="col" style={{ textAlign: 'right' }}>Thao tác</th>}
                </tr>
              </thead>
              <tbody>
                {project.members.map(member => (
                  <tr key={member.userId}>
                    <td>
                      <strong>{member.username}</strong>
                      {member.userId === currentUser?.id && (
                        <span className="status-badge" style={{ marginLeft: 8, background: 'var(--color-info-bg)', color: 'var(--color-info)' }}>
                          (Bạn)
                        </span>
                      )}
                    </td>
                    <td>{member.fullName}</td>
                    <td>
                      {member.role === 'Leader' ? (
                        <span className="status-badge status-rendered">👑 Trưởng nhóm</span>
                      ) : member.role === 'Lecturer' ? (
                        <span className="status-badge status-pending">🎓 Giảng viên hướng dẫn</span>
                      ) : (
                        <span className="status-badge">👤 Thành viên</span>
                      )}
                    </td>
                    <td>{new Date(member.joinedAt).toLocaleDateString('vi-VN')}</td>
                    {isLeader && (
                      <td className="cell-actions">
                        {member.role !== 'Leader' ? (
                          <button
                            type="button"
                            className="button button-danger button-small"
                            onClick={() => handleRemoveMember(member)}
                            disabled={actionLoading}
                          >
                            Xóa
                          </button>
                        ) : (
                          <span style={{ fontSize: 12, color: '#949daf' }}>Chủ dự án</span>
                        )}
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Tab 2: Sơ đồ UML */}
      {activeTab === 'diagrams' && (
        <div>
          <DiagramListPanel
            projectId={project.id}
            canEdit={!isLecturer && (isLeader || isMember)}
            onSelectDiagram={(diagramId) => navigate(`/diagrams/${diagramId}?projectId=${encodeURIComponent(project.id)}`, diagramNavigation)}
            onCreateNew={() => setShowCreateDiagramModal(true)}
          />
        </div>
      )}

      {/* Tab 3: Thẩm định & Review */}
      {activeTab === 'reviews' && (
        <ReviewPanel
          projectId={project.id}
          projectName={project.name}
          isLeader={isLeader}
          isLecturer={isLecturer}
        />
      )}

      {/* Invite Member Modal */}
      {showInviteModal && (
        <InviteMemberModal
          projectId={project.id}
          projectName={project.name}
          currentMemberCount={studentMemberCount}
          onClose={() => setShowInviteModal(false)}
          onSuccess={() => {
            setShowInviteModal(false);
            notify('Đã gửi lời mời tham gia dự án thành công!');
            fetchProject();
          }}
        />
      )}

      {/* Assign Lecturer Modal */}
      {showLecturerModal && (
        <Modal title="Gán Giảng viên hướng dẫn" onClose={() => setShowLecturerModal(false)} busy={actionLoading} className="feature-page">
            <p className="muted" style={{ marginBottom: 16 }}>
              Theo quy chuẩn <strong>BR-10</strong>, chỉ những tài khoản có vai trò <strong>Lecturer</strong> trong Danh bạ nhân bản mới được gán làm GVHD cho dự án.
            </p>

            {message && <LoadError message={message.text} onRetry={openLecturerModal} />}
            {loadingLecturers ? (
              <LoadingState label="Đang tải danh sách giảng viên…" />
            ) : (
              <form onSubmit={handleAssignLecturer} aria-busy={actionLoading}><fieldset disabled={actionLoading}>
                <div style={{ marginBottom: 20 }}>
                  {lecturers.length === 0 && !message && <p role="status">Chưa có giảng viên trong danh bạ. Hãy thử lại sau.</p>}
                  <label className="field-label" htmlFor="select-lecturer">
                    Chọn Giảng viên hướng dẫn
                  </label>
                  <select
                    id="select-lecturer" data-autofocus
                    className="select"
                    value={selectedLecturerId}
                    disabled={loadingLecturers || lecturers.length === 0}
                    onChange={e => setSelectedLecturerId(e.target.value)}
                    style={{ width: '100%', maxWidth: '100%' }}
                    required
                  >
                    {lecturers.map(l => (
                      <option key={l.userId} value={l.userId}>
                        {l.fullName} ({l.username})
                      </option>
                    ))}
                  </select>
                </div>

                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
                  <button
                    type="button"
                    className="button button-secondary"
                    onClick={() => setShowLecturerModal(false)}
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    className="button button-primary"
                    disabled={actionLoading || !selectedLecturerId}
                  >
                    {actionLoading ? 'Đang gán...' : 'Xác nhận gán GVHD'}
                  </button>
                </div>
              </fieldset></form>
            )}
        </Modal>
      )}
      {showCreateDiagramModal && <CreateDiagramModal
        projectId={project.id}
        onClose={() => setShowCreateDiagramModal(false)}
        onCreated={created => navigate(`/diagrams/${created.id}?projectId=${encodeURIComponent(project.id)}`, diagramNavigation)}
      />}
    </div>
  );
}

