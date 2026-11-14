import { Modal } from '../../shared/Modal';
import { useToast } from '../../shared/Toast';
import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { useLocation, useNavigate } from 'react-router';
import { useAuth } from '../../auth/AuthContext';
import { DiagramTypeLabels } from './types';
import type { DiagramDetail, DiagramVersionSummary, DiagramVersionDetail, Comment } from './types';
import { diagramApi } from './diagramApi';
import { useRenderStatus } from '../../shared/useRenderStatus';
import { ApiError } from '../../shared/api';
import type { RenderSnapshot } from '../../shared/useRenderStatus';

interface DiagramViewPageProps {
  diagramId: string;
  projectId?: string;
  onBack?: (projectId?: string) => void;
  canEdit?: boolean;
}

function sanitizeSvg(svg: string): string {
  if (!svg) return '';
  return svg
    .replace(/<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>/gi, '')
    .replace(/\s*on\w+\s*=\s*(["'][^"']*["']|[^\s>]+)/gi, '');
}

export const DiagramViewPage: React.FC<DiagramViewPageProps> = ({ diagramId, projectId, onBack, canEdit = true }) => {
  const notify = useToast();
  const navigate = useNavigate();
  const location = useLocation();
  const { user } = useAuth();
  const [diagram, setDiagram] = useState<DiagramDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Versions
  const [versions, setVersions] = useState<DiagramVersionSummary[]>([]);
  const [selectedVersion, setSelectedVersion] = useState<DiagramVersionDetail | null>(null);
  const [showVersionsModal, setShowVersionsModal] = useState(false);

  // Comments
  const [comments, setComments] = useState<Comment[]>([]);
  const [newComment, setNewComment] = useState('');
  const [submittingComment, setSubmittingComment] = useState(false);

  // Zoom & View
  const [zoom, setZoom] = useState(1);
  const [fitToScreen, setFitToScreen] = useState(true);

  const fetchDiagram = useCallback(async () => {
    try {
      const { diagram: data } = await diagramApi.getDiagramById(diagramId);
      if (projectId && data.projectId !== projectId) throw new Error('Sơ đồ không thuộc dự án đang mở.');
      setDiagram(data);
      return data;
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Không thể tải thông tin sơ đồ.');
      return null;
    }
  }, [diagramId, projectId]);

  const fetchVersions = useCallback(async () => {
    try {
      const res = await diagramApi.getVersions(diagramId);
      setVersions(res.items);
    } catch {
      // ignore
    }
  }, [diagramId]);

  const fetchComments = useCallback(async () => {
    try {
      const res = await diagramApi.getComments(diagramId);
      setComments(res);
    } catch {
      // ignore
    }
  }, [diagramId]);

  // Initial load
  useEffect(() => {
    setLoading(true);
    Promise.all([fetchDiagram(), fetchVersions(), fetchComments()]).finally(() => setLoading(false));
  }, [fetchDiagram, fetchVersions, fetchComments]);

  const initialSnapshot = useMemo<RenderSnapshot | null>(() => {
    if (!diagram) return null;
    return {
      status: diagram.status,
      svg: diagram.svgXml ?? null,
      plantUml: diagram.plantUml ?? null,
      error: diagram.lastError ?? null,
      version: diagram.currentVersion,
      renderedAt: diagram.renderedAt ?? null,
      etag: diagram.eTag ?? null,
    };
  }, [diagram?.id]);

  const renderState = useRenderStatus(diagramId, initialSnapshot);

  useEffect(() => {
    if (!renderState.snapshot) return;
    setDiagram(prev => prev ? {
      ...prev,
      status: renderState.snapshot!.status,
      svgXml: renderState.snapshot!.svg,
      plantUml: renderState.snapshot!.plantUml,
      lastError: renderState.snapshot!.error,
      currentVersion: renderState.snapshot!.version,
      renderedAt: renderState.snapshot!.renderedAt,
      eTag: renderState.snapshot!.etag,
    } : null);
    if (renderState.snapshot.status === 'Rendered' || renderState.snapshot.status === 'Failed') {
      fetchVersions();
    }
  }, [renderState.snapshot, fetchVersions]);

  const handleRender = async () => {
    try {
      await diagramApi.requestRender(diagramId);
      renderState.track();
      notify('Đã gửi yêu cầu render.');
    } catch (err: unknown) {
      if (err instanceof ApiError && err.status === 409) {
        notify(
          err.message || 'Phiên bản này đã được render hoặc đã thất bại. Để render lại, hãy chỉnh sửa để tạo phiên bản mới hoặc sử dụng chức năng Retry của Admin.',
          'error'
        );
      } else {
        notify(err instanceof Error ? err.message : 'Lỗi khi yêu cầu render.', 'error');
      }
    }
  };

  const handleDownloadSvg = () => {
    if (!diagram?.svgXml) return;
    const blob = new Blob([diagram.svgXml], { type: 'image/svg+xml;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `${diagram.title.toLowerCase().replace(/[^a-z0-9_-]+/gi, '_') || 'diagram'}.svg`;
    link.click();
    window.setTimeout(() => URL.revokeObjectURL(url), 1000);
    notify('Đã tải file SVG.');
  };

  const handleViewVersion = async (versionNumber: number) => {
    try {
      const detail = await diagramApi.getVersionDetail(diagramId, versionNumber);
      setSelectedVersion(detail);
      setShowVersionsModal(true);
    } catch (err: unknown) {
      notify(err instanceof Error ? err.message : 'Không thể tải thông tin phiên bản.', 'error');
    }
  };

  const handleRestoreVersion = async (versionNumber: number) => {
    if (!window.confirm(`Bạn có chắc muốn khôi phục sơ đồ về phiên bản v${versionNumber}? Hệ thống sẽ tạo một phiên bản mới từ bản này.`)) {
      return;
    }
    try {
      await diagramApi.restoreVersion(diagramId, versionNumber);
      setShowVersionsModal(false);
      await fetchDiagram();
      notify(`Đã khôi phục thành công về phiên bản v${versionNumber}!`);
    } catch (err: unknown) {
      notify(err instanceof Error ? err.message : 'Không thể khôi phục phiên bản.', 'error');
    }
  };

  const handleAddComment = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newComment.trim()) return;

    try {
      setSubmittingComment(true);
      const created = await diagramApi.addComment(diagramId, newComment.trim());
      setComments((prev) => [...prev, created]);
      setNewComment('');
      notify('Đã gửi góp ý.');
    } catch (err: unknown) {
      notify(err instanceof Error ? err.message : 'Không thể gửi bình luận.', 'error');
    } finally {
      setSubmittingComment(false);
    }
  };

  if (loading) return <div className="panel skeleton" style={{ height: '400px' }}>Đang tải sơ đồ...</div>;
  if (error || !diagram) return <div className="notice danger">{error || 'Không tìm thấy sơ đồ.'}</div>;

  return (
    <div className="diagram-view-page" style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
      {/* Top Header */}
      <div className="panel" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          {onBack && (
            <button className="button button-ghost button-small" onClick={() => onBack ? onBack(diagram.projectId) : navigate(`/projects/${diagram.projectId}`)} style={{ marginBottom: '8px' }}>
              ← Quay lại danh sách
            </button>
          )}
          <h1>{diagram.title}</h1>
          <div style={{ display: 'flex', gap: '12px', alignItems: 'center', marginTop: '6px' }}>
            <span className="chip-btn">{DiagramTypeLabels[diagram.type]}</span>
            <span>Phiên bản: <strong>v{diagram.currentVersion}</strong></span>
            {diagram.status === 'Rendered' && <span className="status-badge status-success">✓ Đã render</span>}
            {diagram.status === 'Rendering' && <span className="status-badge status-info">⟳ Đang render…</span>}
            {diagram.status === 'Pending' && <span className="status-badge status-warning">⏳ Chờ render</span>}
            {diagram.status === 'Failed' && <span className="status-badge status-danger">✕ Render lỗi</span>}
            {diagram.status === 'Draft' && <span className="status-badge status-draft">✎ Nháp</span>}
          </div>
        </div>

        <div style={{ display: 'flex', gap: '8px' }}>
          {canEdit && (
            <button
              className="button button-secondary"
              onClick={() => navigate(`/diagrams/${diagramId}/edit?projectId=${encodeURIComponent(diagram.projectId)}`, { state: location.state })}
            >
              ✏️ Chỉnh sửa sơ đồ
            </button>
          )}
          {canEdit && diagram.status !== 'Rendered' && (
            <button
              className="button button-primary"
              onClick={handleRender}
              disabled={diagram.status === 'Pending' || diagram.status === 'Rendering'}
            >
              {diagram.status === 'Rendering' ? 'Đang render…' : 'Render sơ đồ'}
            </button>
          )}
          <button className="button button-secondary" onClick={() => setShowVersionsModal(true)}>
            Lịch sử phiên bản ({versions.length})
          </button>
        </div>
      </div>

      {diagram.status === 'Failed' && diagram.lastError && (
        <div className="notice danger">
          <strong>Lỗi render:</strong> {diagram.lastError} (SVG hiển thị dưới đây là của phiên bản hợp lệ gần nhất)
        </div>
      )}

      {diagram.needsResubmit && (
        <div className="notice warning" style={{ marginBottom: '16px', background: '#fffbeb', border: '1px solid #fde68a', color: '#92400e', padding: '12px 16px', borderRadius: '6px' }}>
          <strong>⚠️ Chú ý:</strong> Sơ đồ này đã được chỉnh sửa sau khi đồ án được phê duyệt. Trưởng nhóm cần gửi yêu cầu thẩm định lại (Review) sau khi hoàn tất các thay đổi.
        </div>
      )}

      {/* Main Canvas Area */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 340px', gap: '20px' }}>
        {/* SVG Viewport */}
        <div className="panel" style={{ minHeight: '500px', display: 'flex', flexDirection: 'column' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
            <span style={{ fontWeight: 600 }}>Bản vẽ trực quan (SVG)</span>
            <div style={{ display: 'flex', gap: '6px', alignItems: 'center' }}>
              {diagram.svgXml && (
                <button
                  type="button"
                  className="button button-secondary button-small"
                  aria-label="Tải file ảnh SVG"
                  onClick={handleDownloadSvg}
                >
                  📥 Tải SVG
                </button>
              )}
              <button
                type="button"
                className={`button button-small ${fitToScreen ? 'button-primary' : 'button-secondary'}`}
                aria-label="Chuyển chế độ vừa khung toàn cảnh"
                onClick={() => {
                  setFitToScreen(!fitToScreen);
                  if (!fitToScreen) setZoom(1);
                }}
              >
                {fitToScreen ? '⊡ Vừa khung' : '🔍 Kích thước gốc'}
              </button>
              <button
                type="button"
                className="button button-secondary button-small"
                aria-label="Thu nhỏ sơ đồ"
                onClick={() => {
                  setFitToScreen(false);
                  setZoom((z) => Math.max(0.4, Number((z - 0.2).toFixed(1))));
                }}
              >
                -
              </button>
              <button
                type="button"
                className="button button-secondary button-small"
                aria-label="Đặt lại tỷ lệ 100%"
                onClick={() => {
                  setFitToScreen(false);
                  setZoom(1);
                }}
              >
                {Math.round(zoom * 100)}%
              </button>
              <button
                type="button"
                className="button button-secondary button-small"
                aria-label="Phóng to sơ đồ"
                onClick={() => {
                  setFitToScreen(false);
                  setZoom((z) => Math.min(2.5, Number((z + 0.2).toFixed(1))));
                }}
              >
                +
              </button>
            </div>
          </div>

          <div
            className={`svg-canvas ${fitToScreen ? 'fit-to-screen' : ''}`}
            style={{
              flex: 1,
              overflow: 'auto',
              background: '#ffffff',
              border: '1px solid var(--color-border)',
              borderRadius: '6px',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              padding: '20px',
            }}
          >
            {diagram.svgXml ? (
              <div
                role="img"
                aria-label={diagram.title}
                style={fitToScreen ? { maxWidth: '100%', maxHeight: '100%', display: 'flex', justifyContent: 'center' } : { transform: `scale(${zoom})`, transformOrigin: 'center center', transition: 'transform 0.15s ease' }}
                dangerouslySetInnerHTML={{ __html: sanitizeSvg(diagram.svgXml) }}
              />
            ) : (
              <div className="empty-state">
                <p>Sơ đồ chưa được vẽ. Bấm "Render sơ đồ" để bắt đầu.</p>
                {canEdit && (
                  <button className="button button-primary" onClick={handleRender}>
                    Render ngay
                  </button>
                )}
              </div>
            )}
          </div>
        </div>

        {/* Comments Sidebar */}
        <div className="panel" style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
          <h3>Góp ý & Bình luận ({comments.length})</h3>
          <div className="comment-list" style={{ flex: 1, overflowY: 'auto', margin: '12px 0', display: 'flex', flexDirection: 'column', gap: '10px' }}>
            {comments.length === 0 ? (
              <p style={{ color: 'var(--color-muted)', fontSize: '13px' }}>Chưa có bình luận nào.</p>
            ) : (
              comments.map((c) => (
                <div key={c.id} style={{ background: '#f8f9fc', padding: '10px', borderRadius: '6px', border: '1px solid var(--color-border)' }}>
                  <div style={{ fontSize: '12px', color: 'var(--color-muted)', marginBottom: '4px' }}>
                    {new Date(c.createdAt).toLocaleString('vi-VN')}
                  </div>
                  <div style={{ fontSize: '14px', whiteSpace: 'pre-wrap' }}>{c.content}</div>
                </div>
              ))
            )}
          </div>

          <form onSubmit={handleAddComment}>
            <label htmlFor={`diagram-comment-${diagramId}`}>Nội dung góp ý</label>
            <textarea
              id={`diagram-comment-${diagramId}`}
              className="input"
              rows={3}
              placeholder="Nhập nội dung góp ý hoặc bình luận..."
              value={newComment}
              onChange={(e) => setNewComment(e.target.value)}
              style={{ width: '100%', marginBottom: '8px' }}
            />
            <button
              type="submit"
              className="button button-primary button-small"
              disabled={submittingComment || !newComment.trim()}
              style={{ width: '100%' }}
            >
              {submittingComment ? 'Đang gửi…' : 'Gửi góp ý'}
            </button>
          </form>
        </div>
      </div>

      {/* Version History Modal */}
      {showVersionsModal && (
        <Modal
          title={`Lịch sử phiên bản: ${diagram.title}`}
          onClose={() => {
            setShowVersionsModal(false);
            setSelectedVersion(null);
          }}
        >

            <div style={{ maxHeight: '70vh', overflowY: 'auto' }}>
              {versions.length === 0 ? (
                <p style={{ color: 'var(--color-muted, #5f6c87)', padding: '16px 0' }}>Chưa có phiên bản nào.</p>
              ) : (
                <table className="data-table" style={{ width: '100%' }}>
                <thead>
                  <tr>
                    <th>Phiên bản</th>
                    <th>Thời gian lưu</th>
                    <th>Người lưu</th>
                    <th>Hành động</th>
                  </tr>
                </thead>
                <tbody>
                  {versions.map((v) => (
                    <tr key={v.id}>
                      <td>v{v.version}</td>
                      <td>{new Date(v.createdAt).toLocaleString('vi-VN')}</td>
                      <td>{v.createdBy.toLowerCase() === user?.id?.toLowerCase() ? `${user.username || user.fullName || 'Tôi'} (Bạn)` : `${v.createdBy.substring(0, 8)}…`}</td>
                      <td>
                        <div style={{ display: 'flex', gap: '6px' }}>
                          <button className="button button-secondary button-small" onClick={() => handleViewVersion(v.version)}>
                            Xem JSON
                          </button>
                          {canEdit && v.version !== diagram.currentVersion && (
                            <button className="button button-ghost button-small" onClick={() => handleRestoreVersion(v.version)}>
                              Khôi phục
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              )}

              {selectedVersion && (
                <div style={{ marginTop: '20px', borderTop: '1px solid var(--color-border, #e0e5ef)', paddingTop: '12px' }}>
                  <h4 style={{ margin: '0 0 8px 0', fontSize: '14px', color: 'var(--color-text, #202735)' }}>Chi tiết snapshot v{selectedVersion.version}</h4>
                  <pre style={{ background: '#f8f9fc', color: '#202735', border: '1px solid #e0e5ef', padding: '12px', borderRadius: '6px', maxHeight: '220px', overflow: 'auto', fontSize: '12px', fontFamily: 'monospace', lineHeight: '1.4' }}>
                    {selectedVersion.modelJson}
                  </pre>
                </div>
              )}
            </div>
        </Modal>
      )}
    </div>
  );
};
