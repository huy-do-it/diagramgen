import { apiFetch, apiRequest } from '../../shared/api';
import type { PagedResult } from '../../shared/api';
import type {
  AssignLecturerRequest,
  CreateProjectRequest,
  InvitationItem,
  InviteMemberRequest,
  ProjectDetail,
  ProjectMemberItem,
  ProjectRole,
  ProjectSummary,
  UpdateProjectRequest,
  UserDirectoryItem,
} from './types';

export const workspaceApi = {
  async getProjects(params?: {
    search?: string;
    role?: ProjectRole | '';
    page?: number;
    pageSize?: number;
    sort?: string;
  }): Promise<PagedResult<ProjectSummary>> {
    const query = new URLSearchParams();
    if (params?.search) query.set('search', params.search);
    if (params?.role) query.set('role', params.role);
    if (params?.page) query.set('page', String(params.page));
    if (params?.pageSize) query.set('pageSize', String(params.pageSize));
    if (params?.sort) query.set('sort', params.sort);

    const queryString = query.toString();
    const endpoint = queryString ? `/projects?${queryString}` : '/projects';
    return apiRequest<PagedResult<ProjectSummary>>(endpoint);
  },

  async getProject(id: string): Promise<{ project: ProjectDetail; etag: string | null }> {
    const response = await apiFetch(`/projects/${id}`);
    const etag = response.headers.get('ETag');
    const project = (await response.json()) as ProjectDetail;
    return { project, etag };
  },

  async createProject(data: CreateProjectRequest): Promise<ProjectDetail> {
    return apiRequest<ProjectDetail>('/projects', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  },

  async updateProject(id: string, data: UpdateProjectRequest, etag?: string | null): Promise<ProjectDetail> {
    const headers: Record<string, string> = {};
    if (etag) {
      headers['If-Match'] = etag;
    }
    return apiRequest<ProjectDetail>(`/projects/${id}`, {
      method: 'PUT',
      headers,
      body: JSON.stringify(data),
    });
  },

  async deleteProject(id: string): Promise<void> {
    return apiRequest<void>(`/projects/${id}`, {
      method: 'DELETE',
    });
  },

  async getMembers(projectId: string): Promise<ProjectMemberItem[]> {
    return apiRequest<ProjectMemberItem[]>(`/projects/${projectId}/members`);
  },

  async removeMember(projectId: string, userId: string): Promise<void> {
    return apiRequest<void>(`/projects/${projectId}/members/${userId}`, {
      method: 'DELETE',
    });
  },

  async assignLecturer(projectId: string, lecturerId: string): Promise<ProjectDetail> {
    return apiRequest<ProjectDetail>(`/projects/${projectId}/lecturer`, {
      method: 'PUT',
      body: JSON.stringify({ lecturerId } as AssignLecturerRequest),
    });
  },

  async getDirectoryUsers(params?: {
    search?: string;
    role?: string;
    page?: number;
    pageSize?: number;
  }): Promise<PagedResult<UserDirectoryItem>> {
    const query = new URLSearchParams();
    if (params?.search) query.set('search', params.search);
    if (params?.role) query.set('role', params.role);
    if (params?.page) query.set('page', String(params.page));
    if (params?.pageSize) query.set('pageSize', String(params.pageSize));

    const queryString = query.toString();
    const endpoint = queryString ? `/directory/users?${queryString}` : '/directory/users';
    return apiRequest<PagedResult<UserDirectoryItem>>(endpoint);
  },

  async inviteMember(projectId: string, username: string): Promise<InvitationItem> {
    return apiRequest<InvitationItem>(`/projects/${projectId}/invitations`, {
      method: 'POST',
      body: JSON.stringify({ username } as InviteMemberRequest),
    });
  },

  async getMyInvitations(): Promise<InvitationItem[]> {
    return apiRequest<InvitationItem[]>('/invitations/mine');
  },

  async acceptInvitation(invitationId: string): Promise<void> {
    return apiRequest<void>(`/invitations/${invitationId}/accept`, {
      method: 'POST',
    });
  },

  async declineInvitation(invitationId: string): Promise<void> {
    return apiRequest<void>(`/invitations/${invitationId}/decline`, {
      method: 'POST',
    });
  },
};

