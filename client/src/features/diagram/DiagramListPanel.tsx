import React, { useState, useEffect, useCallback } from 'react';
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
  const [diagrams, setDiagrams] = useState<DiagramSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [selectedType, setSelectedType] = useState<number | undefined>();
  const [selectedStatus, setSelectedStatus] = useState<string | undefined>();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
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
      setError(err instanceof Error ? err.message : 'Không thể tải danh sách sơ đồ.');
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
      fetchDiagrams();
    } catch (err: unknown) {
      alert(err instanceof Error ? err.message : 'Lỗi khi gửi yêu cầu render.');
    }
  };

  const handleDelete = async (e: React.MouseEvent, id: string) => {
    e.stopPropagation();
    if (!window.confirm('Bạn có chắc muốn xóa sơ đồ này không?')) return;
    try {
      await diagramApi.deleteDiagram(id);
      fetchDiagrams();
    } catch (err: unknown) {
      alert(err instanceof Error ? err.message : 'Lỗi khi xóa sơ đồ.');
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
            setSearch(e.target.value);
            setPage(1);
          }}
          style={{ maxWidth: '240px' }}
        />

        <select
          className="input"
          value={selectedType || ''}
          onChange={(e) => {
            setSelectedType(e.target.value ? Number(e.target.value) : undefined);
            setPage(1);
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
            setSelectedStatus(e.target.value || undefined);
            setPage(1);
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
                        className="button button-primary button-small"
                        onClick={(e) => handleRender(e, d.id)}
                        disabled={d.status === 'Pending' || d.status === 'Rendering'}
                        style={{ marginRight: '8px' }}
                      >
                        Render
                      </button>
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
            onClick={() => setPage((p) => Math.max(1, p - 1))}
          >
            « Trang trước
          </button>
          <span style={{ alignSelf: 'center' }}>
            Trang {page} / {totalPages}
          </span>
          <button
            className="button button-secondary button-small"
            disabled={page >= totalPages}
            onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
          >
            Trang sau »
          </button>
        </div>
      )}
    </div>
  );
};
