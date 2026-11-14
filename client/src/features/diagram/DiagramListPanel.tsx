import { useToast } from '../../shared/Toast';
import React, { useState, useEffect, useCallback } from 'react';
import { useLocation, useNavigate, useSearchParams } from 'react-router';
import { ApiError } from '../../shared/api';
import { DiagramTypeLabels } from './types';
import type { DiagramSummary, DiagramStatus } from './types';
import { diagramApi } from './diagramApi';

interface DiagramListPanelProps {
  projectId: string;
  onSelectDiagram?: (diagramId: string) => void;
  onCreateNew?: () => void;
  canEdit?: boolean;
}

export const DiagramListPanel: React.FC<DiagramListPanelProps> = ({
  projectId,
  onSelectDiagram,
  onCreateNew,
  canEdit = true,
}) => {
  const notify = useToast();
  const navigate = useNavigate();
  const location = useLocation();
  const [params, setParams] = useSearchParams();
  const [diagrams, setDiagrams] = useState<DiagramSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const type = Number(params.get('diagramType'));
  const selectedType = Number.isInteger(type) && type >= 1 && type <= 5 ? type : undefined;
  const status = params.get('diagramStatus') ?? '';
  const selectedStatus = ['Draft', 'Pending', 'Rendering', 'Rendered', 'Failed'].includes(status) ? status : undefined;
  const search = params.get('diagramSearch') ?? '';
  const requestedPage = Number(params.get('diagramPage'));
  const page = Number.isSafeInteger(requestedPage) && requestedPage > 0 ? requestedPage : 1;
  function updateFilters(values: Record<string, string>) {
    setParams(previous => {
      const next = new URLSearchParams(previous);
      for (const [key, value] of Object.entries(values)) {
        if (value) next.set(key, value);
        else next.delete(key);
      }
      return next;
    }, { replace: true });
  }
  const [totalPages, setTotalPages] = useState(1);
  const [totalItems, setTotalItems] = useState(0);

  const fetchDiagrams = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const res = await diagramApi.getDiagrams({
        projectId,
        type: selectedType,
        status: selectedStatus,
        search: search.trim() || undefined,
        page,
        pageSize: 10,
      });
      setDiagrams(res.items);
      setTotalPages(res.totalPages);
      setTotalItems(res.totalItems);
    } catch (err: unknown) {
      if (err instanceof ApiError && err.status === 403) {
        setError('Bạn chưa có quyền truy cập đồ án này hoặc dữ liệu quyền đang được đồng bộ từ Workspace. Vui lòng thử lại sau giây lát nếu bạn vừa được thêm vào nhóm.');
      } else {
        setError(err instanceof Error ? err.message : 'Không thể tải danh sách sơ đồ.');
      }
    } finally {
      setLoading(false);
    }
  }, [projectId, selectedType, selectedStatus, search, page]);

  useEffect(() => {
    fetchDiagrams();
  }, [fetchDiagrams]);

  const handleRender = async (e: React.MouseEvent, id: string) => {
    e.stopPropagation();
    try {
      await diagramApi.requestRender(id);
      notify('Đã gửi yêu cầu render.');
      fetchDiagrams();
    } catch (err: unknown) {
      notify(err instanceof Error ? err.message : 'Lỗi khi gửi yêu cầu render.', 'error');
    }
  };

  const handleDelete = async (e: React.MouseEvent, id: string) => {
    e.stopPropagation();
    if (!window.confirm('Bạn có chắc muốn xóa sơ đồ này không?')) return;
    try {
      await diagramApi.deleteDiagram(id);
      notify('Đã xóa sơ đồ.');
      fetchDiagrams();
    } catch (err: unknown) {
      notify(err instanceof Error ? err.message : 'Lỗi khi xóa sơ đồ.', 'error');
    }
  };

  const getStatusBadge = (status: DiagramStatus) => {
    switch (status) {
      case 'Rendered':
        return <span className="status-badge status-success">✓ Đã render</span>;
      case 'Rendering':
        return <span className="status-badge status-info">⟳ Đang render…</span>;
      case 'Pending':
        return <span className="status-badge status-warning">⏳ Chờ render</span>;
      case 'Failed':
        return <span className="status-badge status-danger">✕ Lỗi render</span>;
      default:
        return <span className="status-badge status-draft">✎ Nháp</span>;
    }
  };

  return (
    <div className="panel diagram-list-panel">
      <div className="panel-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2>Danh sách sơ đồ UML ({totalItems})</h2>
        {canEdit && onCreateNew && (
          <button className="button button-primary" onClick={onCreateNew}>
            + Tạo sơ đồ mới
          </button>
        )}
      </div>

      {/* Filters Bar */}
      <div className="filters-bar" style={{ display: 'flex', gap: '12px', flexWrap: 'wrap', margin: '16px 0' }}>
        <input
          type="text"
          className="input"
          placeholder="Tìm theo tên sơ đồ..."
          value={search}
          onChange={(e) => {
            updateFilters({ diagramSearch: e.target.value, diagramPage: '' });
          }}
          style={{ maxWidth: '240px' }}
        />

        <select
          className="input"
          value={selectedType || ''}
          onChange={(e) => {
            updateFilters({ diagramType: e.target.value, diagramPage: '' });
          }}
          style={{ maxWidth: '180px' }}
        >
          <option value="">Tất cả loại sơ đồ</option>
          {Object.entries(DiagramTypeLabels).map(([val, label]) => (
            <option key={val} value={val}>
              {label}
            </option>
          ))}
        </select>

        <select
          className="input"
          value={selectedStatus || ''}
          onChange={(e) => {
            updateFilters({ diagramStatus: e.target.value, diagramPage: '' });
          }}
          style={{ maxWidth: '160px' }}
        >
          <option value="">Tất cả trạng thái</option>
          <option value="Draft">Nháp</option>
          <option value="Pending">Chờ render</option>
          <option value="Rendering">Đang render</option>
          <option value="Rendered">Đã render</option>
          <option value="Failed">Render lỗi</option>
        </select>
      </div>

      {error && <div className="notice danger">{error}</div>}

      {/* Table */}
      {loading ? (
        <div className="skeleton" style={{ height: '200px' }}>Đang tải...</div>
      ) : diagrams.length === 0 ? (
        <div className="empty-state" style={{ textAlign: 'center', padding: '40px' }}>
          <p>Chưa có sơ đồ nào trong dự án.</p>
          {canEdit && onCreateNew && (
            <button className="button button-primary" onClick={onCreateNew}>
              Tạo sơ đồ đầu tiên
            </button>
          )}
        </div>
      ) : (
        <table className="data-table" style={{ width: '100%' }}>
          <thead>
            <tr>
              <th>Tên sơ đồ</th>
              <th>Loại sơ đồ</th>
              <th>Trạng thái</th>
              <th>Phiên bản</th>
              <th>Cập nhật</th>
              <th style={{ textAlign: 'right' }}>Hành động</th>
            </tr>
          </thead>
          <tbody>
            {diagrams.map((d) => (
              <tr
                key={d.id}
                onClick={() => onSelectDiagram?.(d.id)}
                style={{ cursor: onSelectDiagram ? 'pointer' : 'default' }}
              >
                <td>
                  <strong>{d.title}</strong>
                </td>
                <td>{DiagramTypeLabels[d.type] || d.type}</td>
                <td>{getStatusBadge(d.status)}</td>
                <td>v{d.currentVersion}</td>
                <td>{new Date(d.updatedAt).toLocaleString('vi-VN')}</td>
                <td style={{ textAlign: 'right' }}>
                  <button
                    className="button button-secondary button-small"
                    onClick={(e) => {
                      e.stopPropagation();
                      onSelectDiagram?.(d.id);
                    }}
                    style={{ marginRight: '8px' }}
                  >
                    Xem
                  </button>
                  {canEdit && (
                    <>
                      <button
                        className="button button-secondary button-small"
                        onClick={(e) => {
                          e.stopPropagation();
                          navigate(`/diagrams/${d.id}/edit?projectId=${encodeURIComponent(projectId)}`, {
                            state: { diagramListPath: location.pathname + location.search },
                          });
                        }}
                        style={{ marginRight: '8px' }}
                      >
                        Sửa
                      </button>
                      {d.status !== 'Rendered' && (
                        <button
                          className="button button-primary button-small"
                          onClick={(e) => handleRender(e, d.id)}
                          disabled={d.status === 'Pending' || d.status === 'Rendering'}
                          style={{ marginRight: '8px' }}
                        >
                          Render
                        </button>
                      )}
                      <button
                        className="button button-danger button-small"
                        onClick={(e) => handleDelete(e, d.id)}
                      >
                        Xóa
                      </button>
                    </>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="pagination" style={{ display: 'flex', justifyContent: 'center', gap: '8px', marginTop: '16px' }}>
          <button
            className="button button-secondary button-small"
            disabled={page <= 1}
            onClick={() => updateFilters({ diagramPage: String(Math.max(1, page - 1)) })}
          >
            « Trang trước
          </button>
          <span style={{ alignSelf: 'center' }}>
            Trang {page} / {totalPages}
          </span>
          <button
            className="button button-secondary button-small"
            disabled={page >= totalPages}
            onClick={() => updateFilters({ diagramPage: String(Math.min(totalPages, page + 1)) })}
          >
            Trang sau »
          </button>
        </div>
      )}
    </div>
  );
};
