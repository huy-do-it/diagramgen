import { useState, useEffect, useRef } from 'react';
import type { ReactElement } from 'react';
import { Route, useNavigate, useParams, useSearchParams } from 'react-router';
import { useAuth } from '../../auth/AuthContext';
import { DiagramViewPage } from './DiagramViewPage';
import { DiagramListPanel } from './DiagramListPanel';
import { ReviewPanel } from './ReviewPanel';
import { diagramApi } from './diagramApi';
import { DiagramType } from './types';
import { workspaceApi } from '../workspace/workspaceApi';
import type { ProjectRole } from '../workspace/types';

export const diagramRoutes: ReactElement[] = [
  <Route key="project-diagram-list" path="projects/:projectId/diagrams" element={<DiagramListWrapper />} />,
  <Route key="project-diagram-review" path="projects/:projectId/reviews" element={<ReviewPanelWrapper />} />,
  <Route
    key="diagram-list"
    path="diagrams"
    element={<DiagramStudioLayout><DiagramListWrapper /></DiagramStudioLayout>}
  />,
  <Route
    key="diagram-view"
    path="diagrams/:diagramId"
    element={<DiagramStudioLayout><DiagramViewWrapper /></DiagramStudioLayout>}
  />,
  <Route
    key="diagram-review"
    path="reviews"
    element={<DiagramStudioLayout><ReviewPanelWrapper /></DiagramStudioLayout>}
  />,
];

function DiagramStudioLayout({ children }: { children: React.ReactNode }) {
  return (
    <div style={{ maxWidth: '1280px', margin: '0 auto', padding: '24px' }}>
      {children}
    </div>
  );
}

function DiagramListWrapper() {
  const [params] = useSearchParams();
  const { projectId } = useParams<{ projectId?: string }>();
  return <ProjectDiagramList key={projectId ?? params.get('projectId')} />;
}

function ProjectDiagramList() {
  const mounted = useRef(false);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { user } = useAuth();
  const { projectId: routeProjectId } = useParams<{ projectId?: string }>();
  const projectId = routeProjectId ?? searchParams.get('projectId');

  useEffect(() => {
    if (!projectId) {
      navigate('/projects', { replace: true });
    }
  }, [projectId, navigate]);

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

  if (!projectId) {
    return null;
  }

  const handleCreateSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      setCreateError(null);
      const created = await diagramApi.createDiagram({
        projectId,
        type: newType,
        title: newTitle,
        modelJson: newJson,
      });
      if (!mounted.current) return;
      setShowCreateModal(false);
      navigate(`/diagrams/${created.id}?projectId=${encodeURIComponent(projectId)}`);
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

  return (
    <div>
      <DiagramListPanel
        key={projectId}
        projectId={projectId}
        canEdit={canEdit}
        onSelectDiagram={(id) => { navigate(`/diagrams/${id}?projectId=${encodeURIComponent(projectId)}`); }}
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
  const { diagramId = '' } = useParams<{ diagramId: string }>();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const { user } = useAuth();
  const canEdit = user?.role !== 'Lecturer';

  return (
    <DiagramViewPage
      key={`${diagramId}:${params.get('projectId')}`}
      projectId={params.get('projectId') ?? undefined}
      diagramId={diagramId}
      canEdit={canEdit}
      onBack={(projectId) => {
        if (projectId) navigate(`/projects/${projectId}`);
        else navigate('/projects');
      }}
    />
  );
}

function ReviewPanelWrapper() {
  const [searchParams] = useSearchParams();
  const { projectId: routeProjectId } = useParams<{ projectId?: string }>();
  const projectId = routeProjectId ?? searchParams.get('projectId');
  return <ProjectReview key={projectId} projectId={projectId} />;
}

function ProjectReview({ projectId }: { projectId: string | null }) {
  const navigate = useNavigate();
  const { user } = useAuth();
  const [role, setRole] = useState<ProjectRole | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [revision, setRevision] = useState(0);

  useEffect(() => {
    if (!projectId) {
      navigate('/projects', { replace: true });
      return;
    }
    const controller = new AbortController();
    setRole(null);
    setError(null);
    setLoading(true);
    workspaceApi.getProject(projectId, controller.signal).then(({ project }) => {
      if (controller.signal.aborted) return;
      const membership = project.id === projectId
        ? project.members.find(member => member.userId === user?.id) : undefined;
      if (!membership) {
        setError('Bạn không có quyền truy cập Review của dự án này.');
        return;
      }
      setRole(membership.role);
    }).catch((error: unknown) => {
      if (!controller.signal.aborted) {
        setError(error instanceof Error ? error.message : 'Không tải được quyền truy cập dự án.');
      }
    }).finally(() => {
      if (!controller.signal.aborted) setLoading(false);
    });
    return () => controller.abort();
  }, [projectId, user?.id, navigate, revision]);

  if (!projectId) return null;
  if (loading) return <div className="skeleton" role="status">Đang kiểm tra quyền truy cập dự án…</div>;
  if (error) {
    return <div className="notice danger" role="alert">
      <p>{error}</p>
      <button className="button button-secondary" onClick={() => setRevision(value => value + 1)}>Thử lại</button>
    </div>;
  }

  return (
    <ReviewPanel
      key={projectId}
      projectId={projectId}
      isLeader={role === 'Leader'}
      isLecturer={role === 'Lecturer'}
    />
  );
}

export * from './types';
export * from './diagramApi';
export * from './DiagramListPanel';
export * from './DiagramViewPage';
export * from './ReviewPanel';
