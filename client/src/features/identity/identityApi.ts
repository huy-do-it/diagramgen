import { apiRequest } from '../../shared/api';
import type { PagedResult } from '../../shared/api';
import type { LockUserRequest, RegisterRequest, UpdateRoleRequest, UserItem, UserRole } from './types';

export const identityApi = {
  async register(data: RegisterRequest): Promise<UserItem> {
    return apiRequest<UserItem>('/auth/register', {
      method: 'POST',
      body: JSON.stringify(data),
      authenticated: false,
    });
  },

  async getUsers(params?: {
    search?: string;
    role?: UserRole | '';
    isLocked?: boolean;
    page?: number;
    pageSize?: number;
    sort?: string;
  }): Promise<PagedResult<UserItem>> {
    const query = new URLSearchParams();
    if (params?.search) query.set('search', params.search);
    if (params?.role) query.set('role', params.role);
    if (params?.isLocked !== undefined) query.set('isLocked', String(params.isLocked));
    if (params?.page) query.set('page', String(params.page));
    if (params?.pageSize) query.set('pageSize', String(params.pageSize));
    if (params?.sort) query.set('sort', params.sort);

    const queryString = query.toString();
    const endpoint = queryString ? `/users?${queryString}` : '/users';
    return apiRequest<PagedResult<UserItem>>(endpoint);
  },

  async updateRole(userId: string, role: UserRole): Promise<UserItem> {
    return apiRequest<UserItem>(`/users/${userId}/role`, {
      method: 'PATCH',
      body: JSON.stringify({ role } as UpdateRoleRequest),
    });
  },

  async lockUser(userId: string, isLocked: boolean): Promise<UserItem> {
    return apiRequest<UserItem>(`/users/${userId}/lock`, {
      method: 'PATCH',
      body: JSON.stringify({ isLocked } as LockUserRequest),
    });
  },
};

