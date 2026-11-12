import React, { useState, useEffect, useCallback } from 'react';
import { DiagramTypeLabels } from './types';
import type { DiagramDetail, DiagramVersionSummary, DiagramVersionDetail, Comment } from './types';
import { diagramApi } from './diagramApi';
import { useRenderStatus } from './useRenderStatus';

interface DiagramViewPageProps {
  diagramId: string;
  onBack?: () => void;
  canEdit?: boolean;
}

function sanitizeSvg(svg: string): string {
  if (!svg) return '';
  return svg
    .replace(/<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>/gi, '')
    .replace(/\s*on\w+\s*=\s*(["'][^"']*["']|[^\s>]+)/gi, '');
}

export const DiagramViewPage: React.FC<DiagramViewPageProps> = ({ diagramId, onBack, canEdit = true }) => {
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

  // Zoom
  const [zoom, setZoom] = useState(1);

  const fetchDiagram = useCallback(async () => {
    try {
      const { diagram: data } = await diagramApi.getDiagramById(diagramId);
      setDiagram(data);
      return data;
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Không thể tải thông tin sơ đồ.');
      return null;
    }
  }, [diagramId]);

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

  // Polling hook when status is Pending or Rendering (every 2s)
  useRenderStatus({
    status: diagram?.status,
    onRefresh: async () => {
      const updated = await fetchDiagram();
      if (updated?.status === 'Rendered' || updated?.status === 'Failed') {
        fetchVersions();
      }
    },
    intervalMs: 2000,
  });

  const handleRender = async () => {
    try {
      await diagramApi.requestRender(diagramId);
      fetchDiagram();
    } catch (err: unknown) {
      alert(err instanceof Error ? err.message : 'Lỗi khi yêu cầu render.');
    }
  };

  const handleViewVersion = async (versionNumber: number) => {
    try {
      const detail = await diagramApi.getVersionDetail(diagramId, versionNumber);
      setSelectedVersion(detail);
      setShowVersionsModal(true);
    } catch (err: unknown) {
      alert(err instanceof Error ? err.message : 'Không thể tải thông tin phiên bản.');
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
      alert(`Đã khôi phục thành công về phiên bản v${versionNumber}!`);
    } catch (err: unknown) {
      alert(err instanceof Error ? err.message : 'Không thể khôi phục phiên bản.');
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
    } catch (err: unknown) {
      alert(err instanceof Error ? err.message : 'Không thể gửi bình luận.');
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
            <button className="button button-ghost button-small" onClick={onBack} style={{ marginBottom: '8px' }}>
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

      {/* Main Canvas Area */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 340px', gap: '20px' }}>
        {/* SVG Viewport */}
        <div className="panel" style={{ minHeight: '500px', display: 'flex', flexDirection: 'column' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '12px' }}>
            <span style={{ fontWeight: 600 }}>Bản vẽ trực quan (SVG)</span>
            <div style={{ display: 'flex', gap: '6px' }}>
              <button className="button button-secondary button-small" onClick={() => setZoom((z) => Math.max(0.4, z - 0.2))}>-</button>
              <button className="button button-secondary button-small" onClick={() => setZoom(1)}>100%</button>
              <button className="button button-secondary button-small" onClick={() => setZoom((z) => Math.min(2.5, z + 0.2))}>+</button>
            </div>
          </div>

          <div
            className="svg-canvas"
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
                style={{ transform: `scale(${zoom})`, transformOrigin: 'center center', transition: 'transform 0.15s ease' }}
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
            <textarea
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
        <div className="modal" style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div className="modal-content panel" style={{ width: '600px', maxHeight: '80vh', display: 'flex', flexDirection: 'column' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <h2>Lịch sử phiên bản: {diagram.title}</h2>
              <button className="button button-ghost" onClick={() => setShowVersionsModal(false)}>✕</button>
            </div>

            <div style={{ flex: 1, overflowY: 'auto' }}>
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
                      <td>{v.createdBy.substring(0, 8)}…</td>
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

              {selectedVersion && (
                <div style={{ marginTop: '20px', borderTop: '1px solid var(--color-border)', paddingTop: '12px' }}>
                  <h4>Chi tiết snapshot v{selectedVersion.version}</h4>
                  <pre style={{ background: '#f6f7fb', padding: '10px', borderRadius: '6px', maxHeight: '200px', overflow: 'auto', fontSize: '12px' }}>
                    {selectedVersion.modelJson}
                  </pre>
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
