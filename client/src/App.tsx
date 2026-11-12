import { BrowserRouter, Navigate, Route, Routes } from 'react-router';
import { AuthProvider } from './auth/AuthContext';
import { RequireAuth } from './auth/RequireAuth';
import { AppLayout } from './components/AppLayout';
import { NotFoundPage } from './shared/NotFoundPage';
import { diagramRoutes } from './features/diagram/routes';
import { editorRoutes } from './features/editor/routes';
import { identityPublicRoutes, identityRoutes } from './features/identity/routes';
import { notificationRoutes } from './features/notification/routes';
import { renderRoutes } from './features/render/routes';
import { workspaceRoutes } from './features/workspace/routes';

// Mỗi feature tự khai báo route trong features/<tên>/routes.tsx — không cần sửa file này.
export default function App() {
  return <BrowserRouter><AuthProvider><Routes>
    {identityPublicRoutes}
    {diagramRoutes}
    <Route element={<RequireAuth />}><Route element={<AppLayout />}>
      <Route index element={<Navigate to="/projects" replace />} />
      {identityRoutes}
      {workspaceRoutes}
      {editorRoutes}
      {renderRoutes}
      {notificationRoutes}
    </Route></Route>
    <Route path="*" element={<NotFoundPage />} />
  </Routes></AuthProvider></BrowserRouter>;
}
