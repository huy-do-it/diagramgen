import type { ReactElement } from 'react';
import { Route } from 'react-router';
import { DiagramEditorPage } from './pages/DiagramEditorPage';

// Bảo (B-06) — UI-06 trình soạn sơ đồ. Mỗi <Route> cần key.
export const editorRoutes: ReactElement[] = [
  <Route key="diagram-new" path="projects/:projectId/diagrams/new" element={<DiagramEditorPage />} />,
  <Route key="diagram-edit" path="diagrams/:diagramId/edit" element={<DiagramEditorPage />} />,
];
