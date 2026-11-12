import React, { useState, useEffect, useCallback } from 'react';
import type { Review, ReviewStatus } from './types';
import { diagramApi } from './diagramApi';

interface ReviewPanelProps {
  projectId: string;
  isLeader?: boolean;
  isLecturer?: boolean;
}

export const ReviewPanel: React.FC<ReviewPanelProps> = ({ projectId, isLeader = false, isLecturer = false }) => {
  const [reviews, setReviews] = useState<Review[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  // Lecturer decision state
  const [decidingId, setDecidingId] = useState<string | null>(null);
  const [note, setNote] = useState('');

  const fetchReviews = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const res = await diagramApi.getReviews(projectId);
      setReviews(res.items);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Không thể tải danh sách duyệt.');
    } finally {
      setLoading(false);
    }
  }, [projectId]);

  useEffect(() => {
    fetchReviews();
  }, [fetchReviews]);

  const handleSubmitReview = async () => {
    try {
      setSubmitting(true);
      await diagramApi.submitReview(projectId);
      alert('Đã gửi toàn bộ sơ đồ cho Giảng viên duyệt thành công!');
      fetchReviews();
    } catch (err: unknown) {
      alert(err instanceof Error ? err.message : 'Lỗi khi gửi duyệt (BR-29: Hãy đảm bảo mọi sơ đồ đã được Render).');
    } finally {
      setSubmitting(false);
    }
  };

  const handleApprove = async (reviewId: string) => {
    try {
      await diagramApi.approveReview(reviewId, note.trim() || undefined);
      alert('Đã phê duyệt bộ sơ đồ thành công!');
      setDecidingId(null);
      setNote('');
      fetchReviews();
    } catch (err: unknown) {
      alert(err instanceof Error ? err.message : 'Lỗi khi phê duyệt.');
    }
  };

  const handleRequestChanges = async (reviewId: string) => {
    if (!note.trim()) {
      alert('Vui lòng nhập lý do hoặc nội dung yêu cầu sửa đổi.');
      return;
    }

    try {
      await diagramApi.requestChangesReview(reviewId, note.trim());
      alert('Đã gửi yêu cầu sửa đổi thành công!');
      setDecidingId(null);
      setNote('');
      fetchReviews();
    } catch (err: unknown) {
      alert(err instanceof Error ? err.message : 'Lỗi khi gửi yêu cầu sửa đổi.');
    }
  };

  const getStatusBadge = (status: ReviewStatus) => {
    switch (status) {
      case 'Approved':
        return <span className="status-badge status-success">✓ Đã duyệt (Approved)</span>;
      case 'ChangesRequested':
        return <span className="status-badge status-danger">✕ Cần sửa đổi</span>;
      default:
        return <span className="status-badge status-warning">⏳ Chờ duyệt (Submitted)</span>;
    }
  };

  return (
    <div className="panel review-panel">
      <div className="panel-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2>Phê duyệt & Đánh giá đồ án</h2>
        <div style={{ display: 'flex', gap: '10px' }}>
          <a
            href={diagramApi.exportZipUrl(projectId)}
            className="button button-secondary"
            download
            target="_blank"
            rel="noreferrer"
          >
            📦 Xuất toàn bộ file ZIP
          </a>

          {isLeader && (
            <button
              className="button button-primary"
              onClick={handleSubmitReview}
              disabled={submitting}
            >
              {submitting ? 'Đang gửi…' : '🚀 Gửi duyệt cho Giảng viên'}
            </button>
          )}
        </div>
      </div>

      {error && <div className="notice danger" style={{ margin: '16px 0' }}>{error}</div>}

      {/* Reviews List */}
      <div style={{ marginTop: '20px' }}>
        <h3>Lịch sử các đợt nộp duyệt</h3>
        {loading ? (
          <div className="skeleton" style={{ height: '150px' }}>Đang tải lịch sử duyệt...</div>
        ) : reviews.length === 0 ? (
          <div className="empty-state" style={{ textAlign: 'center', padding: '30px' }}>
            <p>Chưa có đợt gửi duyệt nào cho dự án này.</p>
            {isLeader && (
              <p style={{ color: 'var(--color-muted)', fontSize: '13px' }}>
                (Lưu ý: Mọi sơ đồ phải ở trạng thái "Đã render" trước khi có thể nộp duyệt theo luật BR-29).
              </p>
            )}
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', marginTop: '12px' }}>
            {reviews.map((r) => (
              <div
                key={r.id}
                style={{
                  border: '1px solid var(--color-border)',
                  borderRadius: '8px',
                  padding: '16px',
                  background: '#ffffff',
                }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <div>
                    <span style={{ fontSize: '12px', color: 'var(--color-muted)' }}>
                      Mã đợt duyệt: {r.id.substring(0, 8)}… · Nộp lúc: {new Date(r.submittedAt).toLocaleString('vi-VN')}
                    </span>
                  </div>
                  <div>{getStatusBadge(r.status)}</div>
                </div>

                {r.note && (
                  <div style={{ marginTop: '10px', background: '#f8f9fc', padding: '10px', borderRadius: '6px' }}>
                    <strong>Ghi chú từ Giảng viên:</strong> {r.note}
                  </div>
                )}

                {/* Lecturer actions */}
                {isLecturer && r.status === 'Submitted' && (
                  <div style={{ marginTop: '16px', borderTop: '1px solid var(--color-border)', paddingTop: '12px' }}>
                    <h4>Đánh giá của Giảng viên hướng dẫn</h4>
                    <textarea
                      className="input"
                      rows={2}
                      placeholder="Nhập nhận xét hoặc yêu cầu sửa đổi (nếu có)..."
                      value={decidingId === r.id ? note : ''}
                      onChange={(e) => {
                        setDecidingId(r.id);
                        setNote(e.target.value);
                      }}
                      style={{ width: '100%', margin: '8px 0' }}
                    />
                    <div style={{ display: 'flex', gap: '10px' }}>
                      <button className="button button-primary button-small" onClick={() => handleApprove(r.id)}>
                        ✓ Phê duyệt (Approve)
                      </button>
                      <button className="button button-danger button-small" onClick={() => handleRequestChanges(r.id)}>
                        ✕ Yêu cầu sửa đổi (Request Changes)
                      </button>
                    </div>
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};
