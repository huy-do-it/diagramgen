export type UserRole = 'Student' | 'Lecturer' | 'Admin';

export interface UserItem {
  id: string;
  username: string;
  fullName: string;
  role: UserRole;
  isLocked: boolean;
  createdAt: string;
}

export interface RegisterRequest {
  username: string;
  password: string;
  fullName: string;
  role?: 'Student' | 'Lecturer';
}

export interface UpdateRoleRequest {
  role: UserRole;
}

export interface LockUserRequest {
  isLocked: boolean;
}

