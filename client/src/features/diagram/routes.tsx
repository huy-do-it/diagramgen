import { useState } from 'react';
import type { ReactElement } from 'react';
import { Route, useNavigate, useParams, useSearchParams } from 'react-router';
import { useAuth } from '../../auth/AuthContext';
import { DiagramViewPage } from './DiagramViewPage';
import { DiagramListPanel } from './DiagramListPanel';
import { ReviewPanel } from './ReviewPanel';
import { diagramApi } from './diagramApi';
import { DiagramType } from './types';

export const diagramRoutes: ReactElement[] = [
  <Route key="diagram-list" path="diagrams" element={<DiagramListWrapper />} />,
  <Route key="project-diagram-list" path="projects/:projectId/diagrams" element={<DiagramListWrapper />} />,
  <Route key="diagram-view" path="diagrams/:diagramId" element={<DiagramViewWrapper />} />,
  <Route key="diagram-review" path="reviews" element={<ReviewPanelWrapper />} />,
  <Route key="project-diagram-review" path="projects/:projectId/reviews" element={<ReviewPanelWrapper />} />,
];

function DiagramListWrapper() {
  const { projectId: routeProjectId } = useParams<{ projectId?: string }>();
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();
  const { user } = useAuth();

  const [projectIdInput, setProjectIdInput] = useState(() => {
    return routeProjectId || searchParams.get('projectId') || sessionStorage.getItem('currentProjectId') || '';
  });

  const effectiveProjectId = routeProjectId || searchParams.get('projectId') || sessionStorage.getItem('currentProjectId') || '';

  const handleSetProject = (id: string) => {
    const trimmed = id.trim();
    if (trimmed) {
      sessionStorage.setItem('currentProjectId', trimmed);
      setSearchParams({ projectId: trimmed });
    }
  };

  const [showCreateModal, setShowCreateModal] = useState(false);
  const [newTitle, setNewTitle] = useState('CampusEats Order Management');
  const [newType, setNewType] = useState<DiagramType>(DiagramType.UseCase);
  const [newJson, setNewJson] = useState(`{
  "systemBoundary": "CampusEats",
  "actors": ["Student", "Restaurant"],
  "useCases": ["Order Food", "Track Order", "Update Menu"],
  "relations": [
    { "from": "Student", "to": "Order Food", "type": "association" },
    { "from": "Student", "to": "Track Order", "type": "association" },
    { "from": "Restaurant", "to": "Update Menu", "type": "association" }
  ]
}`);
  const [createError, setCreateError] = useState<string | null>(null);

  const canEdit = user?.role !== 'Lecturer';

  const handleCreateSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!effectiveProjectId) {
      setCreateError('Vui lòng chọn hoặc nhập Project ID trước khi tạo sơ đồ.');
      return;
    }
    try {
      setCreateError(null);
      const created = await diagramApi.createDiagram({
        projectId: effectiveProjectId,
        type: newType,
        title: newTitle,
        modelJson: newJson,
      });
      setShowCreateModal(false);
      navigate(`/diagrams/${created.id}`);
    } catch (err: unknown) {
      setCreateError(err instanceof Error ? err.message : 'Không thể tạo sơ đồ.');
    }
  };

  const loadInvalidUmlTemplate = () => {
    setNewTitle('Sơ đồ lỗi UML (Association giữa 2 Actor)');
    setNewJson(`{
  "systemBoundary": "CampusEats",
  "actors": ["Student", "Admin"],
  "useCases": ["Login"],
  "relations": [
    { "from": "Student", "to": "Admin", "type": "association" }
  ]
}`);
  };

  if (!effectiveProjectId) {
    return (
      <div style={{ padding: '32px', maxWidth: '640px', margin: '40px auto', background: '#ffffff', borderRadius: '12px', border: '1px solid #e2e8f0', boxShadow: '0 4px 6px -1px rgba(0,0,0,0.05)' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '16px' }}>
          <span style={{ fontSize: '28px' }}>📐</span>
          <div>
            <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 600 }}>Quản lý Sơ đồ Dự án</h3>
            <p style={{ margin: '4px 0 0', color: '#64748b', fontSize: '13px' }}>Nhập mã dự án để tải danh sách sơ đồ và thẩm định</p>
          </div>
        </div>

        <div style={{ display: 'flex', gap: '8px', marginTop: '20px' }}>
          <input
            type="text"
            placeholder="Nhập Project ID (UUID)..."
            value={projectIdInput}
            onChange={(e) => setProjectIdInput(e.target.value)}
            style={{ flex: 1, padding: '10px 14px', border: '1px solid #cbd5e1', borderRadius: '8px', fontSize: '14px' }}
          />
          <button
            type="button"
            onClick={() => handleSetProject(projectIdInput)}
            style={{ padding: '10px 20px', background: '#2563eb', color: '#ffffff', border: 'none', borderRadius: '8px', cursor: 'pointer', fontWeight: 500, fontSize: '14px' }}
          >
            Mở Sơ đồ
          </button>
        </div>
      </div>
    );
  }

  return (
    <div style={{ padding: '16px 0' }}>
      {/* Top Project Context Bar */}
      <div style={{
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: '12px 16px',
        background: '#ffffff',
        border: '1px solid #e2e8f0',
        borderRadius: '8px',
        marginBottom: '20px'
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <span style={{ fontSize: '18px' }}>📁</span>
          <div>
            <span style={{ fontSize: '12px', color: '#64748b' }}>Dự án:</span>
            <div style={{ fontFamily: 'monospace', fontWeight: 600, fontSize: '14px', color: '#1e293b' }}>{effectiveProjectId}</div>
          </div>
        </div>

        <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
          <button
            type="button"
            onClick={() => navigate(`/projects/${effectiveProjectId}/reviews`)}
            style={{ padding: '6px 14px', borderRadius: '6px', border: '1px solid #cbd5e1', background: '#f8fafc', fontSize: '13px', fontWeight: 500, cursor: 'pointer' }}
          >
            ⚖️ Thẩm định & Review
          </button>
          <button
            type="button"
            onClick={() => {
              sessionStorage.removeItem('currentProjectId');
              setSearchParams({});
            }}
            style={{ padding: '6px 12px', borderRadius: '6px', border: 'none', background: 'transparent', color: '#64748b', fontSize: '13px', cursor: 'pointer' }}
          >
            Đổi dự án
          </button>
        </div>
      </div>

      <DiagramListPanel
        projectId={effectiveProjectId}
        canEdit={canEdit}
        onSelectDiagram={(id) => { navigate(`/diagrams/${id}`); }}
        onCreateNew={() => setShowCreateModal(true)}
      />

      {/* Modal Tạo Sơ đồ Mới */}
      {showCreateModal && (
        <div style={{
          position: 'fixed',
          top: 0, left: 0, right: 0, bottom: 0,
          background: 'rgba(15, 23, 42, 0.6)',
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          zIndex: 1000
        }}>
          <div style={{
            background: '#ffffff',
            borderRadius: '12px',
            padding: '28px',
            width: '640px',
            maxWidth: '90%',
            boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1)'
          }}>
            <h3 style={{ margin: '0 0 16px 0', fontSize: '18px', fontWeight: 600 }}>Tạo Sơ đồ UML Mới</h3>

            {createError && (
              <div style={{
                background: '#fef2f2',
                border: '1px solid #fecaca',
                color: '#991b1b',
                padding: '12px',
                borderRadius: '6px',
                marginBottom: '16px',
                fontSize: '14px'
              }}>
                <strong>Lỗi kiểm tra quy tắc:</strong> {createError}
              </div>
            )}

            <form onSubmit={handleCreateSubmit}>
              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '6px' }}>Tiêu đề sơ đồ:</label>
                <input
                  type="text"
                  required
                  value={newTitle}
                  onChange={(e) => setNewTitle(e.target.value)}
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                />
              </div>

              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '6px' }}>Loại sơ đồ:</label>
                <select
                  value={newType}
                  onChange={(e) => setNewType(Number(e.target.value))}
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                >
                  <option value={DiagramType.UseCase}>Use Case Diagram</option>
                  <option value={DiagramType.Sequence}>Sequence Diagram</option>
                  <option value={DiagramType.BusinessContext}>Business Context Diagram</option>
                  <option value={DiagramType.Activity}>Activity Diagram</option>
                  <option value={DiagramType.State}>State Diagram</option>
                </select>
              </div>

              <div style={{ marginBottom: '16px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '6px' }}>
                  <label style={{ fontSize: '13px', fontWeight: 500 }}>Model JSON:</label>
                  <button
                    type="button"
                    onClick={loadInvalidUmlTemplate}
                    style={{ fontSize: '12px', color: '#b91c1c', background: 'none', border: 'none', cursor: 'pointer', textDecoration: 'underline' }}
                  >
                    ⚠️ Nạp mẫu vi phạm luật UML (Test Edge Case)
                  </button>
                </div>
                <textarea
                  rows={8}
                  required
                  value={newJson}
                  onChange={(e) => setNewJson(e.target.value)}
                  style={{ width: '100%', fontFamily: 'monospace', fontSize: '13px', padding: '10px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button
                  type="button"
                  onClick={() => setShowCreateModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: '1px solid #cbd5e1', background: '#ffffff', cursor: 'pointer' }}
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  id="btn-submit-create-diagram"
                  style={{ padding: '8px 20px', borderRadius: '6px', background: '#2563eb', color: '#ffffff', border: 'none', fontWeight: 500, cursor: 'pointer' }}
                >
                  Xác nhận Tạo
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

function DiagramViewWrapper() {
  const { diagramId } = useParams<{ diagramId: string }>();
  const navigate = useNavigate();
  const { user } = useAuth();
  const canEdit = user?.role !== 'Lecturer';

  return (
    <DiagramViewPage
      diagramId={diagramId || ''}
      canEdit={canEdit}
      onBack={() => {
        const storedProjectId = sessionStorage.getItem('currentProjectId');
        if (storedProjectId) {
          navigate(`/projects/${storedProjectId}/diagrams`);
        } else {
          navigate('/diagrams');
        }
      }}
    />
  );
}

function ReviewPanelWrapper() {
  const { projectId: routeProjectId } = useParams<{ projectId?: string }>();
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();
  const { user } = useAuth();

  const [projectIdInput, setProjectIdInput] = useState(() => {
    return routeProjectId || searchParams.get('projectId') || sessionStorage.getItem('currentProjectId') || '';
  });

  const effectiveProjectId = routeProjectId || searchParams.get('projectId') || sessionStorage.getItem('currentProjectId') || '';

  const handleSetProject = (id: string) => {
    const trimmed = id.trim();
    if (trimmed) {
      sessionStorage.setItem('currentProjectId', trimmed);
      setSearchParams({ projectId: trimmed });
    }
  };

  const isLecturer = user?.role === 'Lecturer';
  const isLeader = user?.role === 'Student';

  if (!effectiveProjectId) {
    return (
      <div style={{ padding: '32px', maxWidth: '640px', margin: '40px auto', background: '#ffffff', borderRadius: '12px', border: '1px solid #e2e8f0', boxShadow: '0 4px 6px -1px rgba(0,0,0,0.05)' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '16px' }}>
          <span style={{ fontSize: '28px' }}>⚖️</span>
          <div>
            <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 600 }}>Thẩm định & Review Dự án</h3>
            <p style={{ margin: '4px 0 0', color: '#64748b', fontSize: '13px' }}>Nhập mã dự án để xem hoặc gửi thẩm định kết quả sơ đồ</p>
          </div>
        </div>

        <div style={{ display: 'flex', gap: '8px', marginTop: '20px' }}>
          <input
            type="text"
            placeholder="Nhập Project ID (UUID)..."
            value={projectIdInput}
            onChange={(e) => setProjectIdInput(e.target.value)}
            style={{ flex: 1, padding: '10px 14px', border: '1px solid #cbd5e1', borderRadius: '8px', fontSize: '14px' }}
          />
          <button
            type="button"
            onClick={() => handleSetProject(projectIdInput)}
            style={{ padding: '10px 20px', background: '#2563eb', color: '#ffffff', border: 'none', borderRadius: '8px', cursor: 'pointer', fontWeight: 500, fontSize: '14px' }}
          >
            Mở Review
          </button>
        </div>
      </div>
    );
  }

  return (
    <div style={{ padding: '16px 0' }}>
      <div style={{
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: '12px 16px',
        background: '#ffffff',
        border: '1px solid #e2e8f0',
        borderRadius: '8px',
        marginBottom: '20px'
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <span style={{ fontSize: '18px' }}>📁</span>
          <div>
            <span style={{ fontSize: '12px', color: '#64748b' }}>Dự án:</span>
            <div style={{ fontFamily: 'monospace', fontWeight: 600, fontSize: '14px', color: '#1e293b' }}>{effectiveProjectId}</div>
          </div>
        </div>

        <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
          <button
            type="button"
            onClick={() => navigate(`/projects/${effectiveProjectId}/diagrams`)}
            style={{ padding: '6px 14px', borderRadius: '6px', border: '1px solid #cbd5e1', background: '#f8fafc', fontSize: '13px', fontWeight: 500, cursor: 'pointer' }}
          >
            📋 Xem Sơ đồ
          </button>
          <button
            type="button"
            onClick={() => {
              sessionStorage.removeItem('currentProjectId');
              setSearchParams({});
            }}
            style={{ padding: '6px 12px', borderRadius: '6px', border: 'none', background: 'transparent', color: '#64748b', fontSize: '13px', cursor: 'pointer' }}
          >
            Đổi dự án
          </button>
        </div>
      </div>

      <ReviewPanel
        projectId={effectiveProjectId}
        isLeader={isLeader}
        isLecturer={isLecturer}
      />
    </div>
  );
}

export * from './types';
export * from './diagramApi';
export * from './DiagramListPanel';
export * from './DiagramViewPage';
export * from './ReviewPanel';
