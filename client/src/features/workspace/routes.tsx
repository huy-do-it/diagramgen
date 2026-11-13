import type { ReactElement } from 'react';
import { Route } from 'react-router';
import { ProjectsPage } from './ProjectsPage';
import { ProjectDetailPage } from './ProjectDetailPage';
import { MyInvitationsPage } from './MyInvitationsPage';

/** Các trang Workspace cần đăng nhập, hiển thị trong AppLayout. */
export const workspaceRoutes: ReactElement[] = [
  <Route key="projects" path="projects" element={<ProjectsPage />} />,
  <Route key="project-detail" path="projects/:id" element={<ProjectDetailPage />} />,
  <Route key="my-invitations" path="invitations/mine" element={<MyInvitationsPage />} />,
];
