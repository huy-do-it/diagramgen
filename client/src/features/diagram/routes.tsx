import type { ReactElement } from 'react';

// Thiện (T-07) — UI-07, UI-08. Mỗi <Route> cần key.
// Ví dụ: <Route key="diagram-view" path="diagrams/:diagramId" element={<DiagramViewPage />} />
export const diagramRoutes: ReactElement[] = [];
