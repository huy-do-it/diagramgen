import { useState, useEffect } from 'react';
import type { ReactElement } from 'react';
import { Route, useNavigate, useParams, useSearchParams } from 'react-router';
import { useAuth } from '../../auth/AuthContext';
import { DiagramViewPage } from './DiagramViewPage';
import { DiagramListPanel } from './DiagramListPanel';
import { ReviewPanel } from './ReviewPanel';
import { CreateDiagramModal } from './CreateDiagramModal';
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
  // Trang sửa "diagrams/:diagramId/edit" là editor UI-06 (features/editor/routes.tsx) — không khai báo trùng ở đây
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
  const canEdit = user?.role !== 'Lecturer';

  if (!projectId) {
    return null;
  }

  return (
    <div>
      <DiagramListPanel
        key={projectId}
        projectId={projectId}
        canEdit={canEdit}
        onSelectDiagram={(id) => { navigate(`/diagrams/${id}?projectId=${encodeURIComponent(projectId)}`); }}
        onCreateNew={() => setShowCreateModal(true)}
      />

      {showCreateModal && <CreateDiagramModal
        projectId={projectId}
        onClose={() => setShowCreateModal(false)}
        onCreated={created => navigate(`/diagrams/${created.id}?projectId=${encodeURIComponent(projectId)}`)}
      />}
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
  const [projectName, setProjectName] = useState<string | undefined>();
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
      setProjectName(project.name);
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
      projectName={projectName}
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
