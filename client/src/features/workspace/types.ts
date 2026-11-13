export type ProjectRole = 'Leader' | 'Member' | 'Lecturer';

export interface ProjectSummary {
  id: string;
  name: string;
  description?: string;
  ownerId: string;
  lecturerId?: string;
  currentUserRole: ProjectRole;
  memberCount: number;
  createdAt: string;
  rowVersion: string;
}

export interface ProjectMemberItem {
  projectId: string;
  userId: string;
  username: string;
  fullName: string;
  role: ProjectRole;
  joinedAt: string;
}

export interface ProjectDetail {
  id: string;
  name: string;
  description?: string;
  ownerId: string;
  lecturerId?: string;
  createdAt: string;
  rowVersion: string;
  members: ProjectMemberItem[];
}

export interface UserDirectoryItem {
  userId: string;
  username: string;
  fullName: string;
  role: string;
  isLocked: boolean;
}

export type InvitationStatus = 'Pending' | 'Accepted' | 'Declined' | 'Expired';

export interface InvitationItem {
  id: string;
  projectId: string;
  projectName: string;
  inviteeId: string;
  status: InvitationStatus;
  createdAt: string;
  expiresAt: string;
}

export interface CreateProjectRequest {
  name: string;
  description?: string;
}

export interface UpdateProjectRequest {
  name: string;
  description?: string;
}

export interface InviteMemberRequest {
  username: string;
}

export interface AssignLecturerRequest {
  lecturerId: string;
}

