import { useState } from 'react';
import { Route } from 'react-router';
import { DiagramViewPage } from './DiagramViewPage';
import { DiagramListPanel } from './DiagramListPanel';
import { ReviewPanel } from './ReviewPanel';
import { diagramApi, DEV_TOKENS } from './diagramApi';
import { DiagramType } from './types';

const DEFAULT_PROJECT_ID = '00000000-0000-0000-0000-000000000001';

export const diagramRoutes = [
  <Route
    key="diagram-list"
    path="/diagrams"
    element={<DiagramStudioLayout><DiagramListWrapper /></DiagramStudioLayout>}
  />,
  <Route
    key="diagram-view"
    path="/diagrams/:diagramId"
    element={<DiagramStudioLayout><DiagramViewWrapper /></DiagramStudioLayout>}
  />,
  <Route
    key="diagram-review"
    path="/reviews"
    element={<DiagramStudioLayout><ReviewPanelWrapper /></DiagramStudioLayout>}
  />,
];

function DiagramStudioLayout({ children }: { children: React.ReactNode }) {
  const [currentRole, setCurrentRole] = useState<'Leader' | 'Lecturer' | 'Member'>(() => {
    const saved = localStorage.getItem('currentRole');
    return (saved as 'Leader' | 'Lecturer' | 'Member') || 'Leader';
  });

  const handleRoleChange = (role: 'Leader' | 'Lecturer' | 'Member') => {
    setCurrentRole(role);
    localStorage.setItem('currentRole', role);
    localStorage.setItem('token', DEV_TOKENS[role]);
    window.location.reload();
  };

  const currentPath = window.location.pathname;

  return (
    <div style={{ minHeight: '100vh', background: '#f8fafc', color: '#0f172a' }}>
      {/* Top Navigation Bar */}
      <header style={{
        background: '#ffffff',
        borderBottom: '1px solid #e2e8f0',
        padding: '12px 24px',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        boxShadow: '0 1px 3px rgba(0,0,0,0.05)'
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '24px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }} onClick={() => { window.location.href = '/diagrams'; }}>
            <span style={{ fontSize: '24px' }}>📐</span>
            <span style={{ fontWeight: 700, fontSize: '18px', color: '#1e40af' }}>UMLForge Diagram Studio</span>
          </div>

          <nav style={{ display: 'flex', gap: '8px' }}>
            <a
              href="/diagrams"
              style={{
                padding: '6px 14px',
                borderRadius: '6px',
                textDecoration: 'none',
                fontWeight: 500,
                fontSize: '14px',
                background: currentPath.startsWith('/diagrams') && !currentPath.includes('/diagrams/') ? '#eff6ff' : 'transparent',
                color: currentPath.startsWith('/diagrams') && !currentPath.includes('/diagrams/') ? '#1d4ed8' : '#64748b'
              }}
            >
              📋 Danh sách Sơ đồ
            </a>
            <a
              href="/reviews"
              style={{
                padding: '6px 14px',
                borderRadius: '6px',
                textDecoration: 'none',
                fontWeight: 500,
                fontSize: '14px',
                background: currentPath.startsWith('/reviews') ? '#eff6ff' : 'transparent',
                color: currentPath.startsWith('/reviews') ? '#1d4ed8' : '#64748b'
              }}
            >
              ⚖️ Thẩm định & Review
            </a>
          </nav>
        </div>

        {/* Role Switcher */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <span style={{ fontSize: '13px', color: '#64748b' }}>Vai trò hiện tại:</span>
          <select
            id="role-switcher"
            value={currentRole}
            onChange={(e) => handleRoleChange(e.target.value as any)}
            style={{
              padding: '6px 12px',
              borderRadius: '6px',
              border: '1px solid #cbd5e1',
              background: '#ffffff',
              fontSize: '13px',
              fontWeight: 600,
              color: currentRole === 'Leader' ? '#1d4ed8' : currentRole === 'Lecturer' ? '#7c3aed' : '#059669'
            }}
          >
            <option value="Leader">👑 Sinh viên Trưởng nhóm (Leader)</option>
            <option value="Lecturer">🎓 Giảng viên Thẩm định (Lecturer)</option>
            <option value="Member">👤 Sinh viên Thành viên (Member)</option>
          </select>
        </div>
      </header>

      {/* Main Content Area */}
      <main style={{ maxWidth: '1280px', margin: '0 auto', padding: '24px' }}>
        {children}
      </main>
    </div>
  );
}

function DiagramListWrapper() {
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

  const currentRole = localStorage.getItem('currentRole') || 'Leader';
  const canEdit = currentRole !== 'Lecturer';

  const handleCreateSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      setCreateError(null);
      const created = await diagramApi.createDiagram({
        projectId: DEFAULT_PROJECT_ID,
        type: newType,
        title: newTitle,
        modelJson: newJson,
      });
      setShowCreateModal(false);
      window.location.href = `/diagrams/${created.id}`;
    } catch (err: any) {
      setCreateError(err.message || 'Không thể tạo sơ đồ.');
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
        projectId={DEFAULT_PROJECT_ID}
        canEdit={canEdit}
        onSelectDiagram={(id) => { window.location.href = `/diagrams/${id}`; }}
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
  const segments = window.location.pathname.split('/');
  const diagramId = segments[segments.length - 1] || '';
  const currentRole = localStorage.getItem('currentRole') || 'Leader';
  const canEdit = currentRole !== 'Lecturer';

  return (
    <DiagramViewPage
      diagramId={diagramId}
      canEdit={canEdit}
      onBack={() => { window.location.href = '/diagrams'; }}
    />
  );
}

function ReviewPanelWrapper() {
  const currentRole = localStorage.getItem('currentRole') || 'Leader';
  const isLeader = currentRole === 'Leader';
  const isLecturer = currentRole === 'Lecturer';

  return (
    <ReviewPanel
      projectId={DEFAULT_PROJECT_ID}
      isLeader={isLeader}
      isLecturer={isLecturer}
    />
  );
}

export * from './types';
export * from './diagramApi';
export * from './DiagramListPanel';
export * from './DiagramViewPage';
export * from './ReviewPanel';
