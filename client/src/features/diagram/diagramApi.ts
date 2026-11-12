import { apiFetch, apiRequest } from '../../shared/api';
import { readSession, saveSession } from '../../auth/session';
import type {
  DiagramDetail,
  DiagramSummary,
  DiagramValidationResult,
  DiagramVersionDetail,
  DiagramVersionSummary,
  PagedResult,
  RelationSuggestion,
  RenderResponse,
  Review,
  Comment,
} from './types';

export const DEV_TOKENS = {
  Leader: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMTExMTExMS0xMTExLTExMTEtMTExMS0xMTExMTExMTExMTEiLCJ1bmlxdWVfbmFtZSI6ImxlYWRlcl9kZXYiLCJyb2xlIjoiTGVhZGVyIiwiaXNzIjoidW1sZm9yZ2UiLCJhdWQiOiJ1bWxmb3JnZS1jbGllbnQiLCJleHAiOjIwNTAwMDAwMDB9.6F67ss7Gn-a-9DIynnold2dZ0sjSluvuZvm2A-nbNFg',
  Lecturer: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIyMjIyMjIyMi0yMjIyLTIyMjItMjIyMi0yMjIyMjIyMjIyMjIiLCJ1bmlxdWVfbmFtZSI6ImxlY3R1cmVyX2RldiIsInJvbGUiOiJMZWN0dXJlciIsImlzcyI6InVtbGZvcmdlIiwiYXVkIjoidW1sZm9yZ2UtY2xpZW50IiwiZXhwIjoyMDUwMDAwMDAwfQ.wqZeNzFbk7sxZhvSjMGYspGwvZmoXRvkezmO5NCrFuM',
  Member: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIzMzMzMzMzMy0zMzMzLTMzMzMtMzMzMy0zMzMzMzMzMzMzMzMiLCJ1bmlxdWVfbmFtZSI6Im1lbWJlcl9kZXYiLCJyb2xlIjoiU3R1ZGVudCIsImlzcyI6InVtbGZvcmdlIiwiYXVkIjoidW1sZm9yZ2UtY2xpZW50IiwiZXhwIjoyMDUwMDAwMDAwfQ.yV409fpXptrDCoo4dF2kdViWTOkJsGpu3MATWyFzCU4'
};

/** Đảm bảo phiên làm việc có sẵn cho chế độ demo / dev nếu chưa đăng nhập qua AuthProvider */
export function ensureDevSession(role: keyof typeof DEV_TOKENS = 'Leader') {
  const current = readSession();
  if (!current || current.expiresAt <= Date.now()) {
    saveSession(DEV_TOKENS[role], 'dev-refresh-token', 86400 * 30);
  }
}

export const diagramApi = {
  async getDiagrams(params: {
    projectId: string;
    type?: number;
    status?: string;
    search?: string;
    sort?: string;
    page?: number;
    pageSize?: number;
  }): Promise<PagedResult<DiagramSummary>> {
    ensureDevSession();
    const query = new URLSearchParams();
    query.set('projectId', params.projectId);
    if (params.type) query.set('type', params.type.toString());
    if (params.status) query.set('status', params.status);
    if (params.search) query.set('search', params.search);
    if (params.sort) query.set('sort', params.sort);
    if (params.page) query.set('page', params.page.toString());
    if (params.pageSize) query.set('pageSize', params.pageSize.toString());

    return await apiRequest<PagedResult<DiagramSummary>>(`diagrams?${query.toString()}`);
  },

  async getDiagramById(id: string): Promise<{ diagram: DiagramDetail; etag: string | null }> {
    ensureDevSession();
    const res = await apiFetch(`diagrams/${id}`);
    const etag = res.headers.get('ETag');
    const diagram = (await res.json()) as DiagramDetail;
    return { diagram, etag };
  },

  async createDiagram(data: {
    projectId: string;
    type: number;
    title: string;
    modelJson: string;
  }): Promise<DiagramDetail> {
    ensureDevSession();
    return await apiRequest<DiagramDetail>('diagrams', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  },

  async updateDiagram(
    id: string,
    data: { title: string; modelJson: string },
    ifMatch?: string
  ): Promise<DiagramDetail> {
    ensureDevSession();
    const headers: Record<string, string> = ifMatch ? { 'If-Match': ifMatch } : {};
    return await apiRequest<DiagramDetail>(`diagrams/${id}`, {
      method: 'PUT',
      headers,
      body: JSON.stringify(data),
    });
  },

  async requestRender(id: string): Promise<RenderResponse> {
    ensureDevSession();
    return await apiRequest<RenderResponse>(`diagrams/${id}/render`, {
      method: 'POST',
    });
  },

  async getVersions(
    id: string,
    page = 1,
    pageSize = 20
  ): Promise<PagedResult<DiagramVersionSummary>> {
    ensureDevSession();
    return await apiRequest<PagedResult<DiagramVersionSummary>>(`diagrams/${id}/versions?page=${page}&pageSize=${pageSize}`);
  },

  async getVersionDetail(id: string, version: number): Promise<DiagramVersionDetail> {
    ensureDevSession();
    return await apiRequest<DiagramVersionDetail>(`diagrams/${id}/versions/${version}`);
  },

  async restoreVersion(id: string, version: number): Promise<DiagramDetail> {
    ensureDevSession();
    return await apiRequest<DiagramDetail>(`diagrams/${id}/versions/${version}/restore`, {
      method: 'POST',
    });
  },

  async validateDiagram(id: string): Promise<DiagramValidationResult> {
    ensureDevSession();
    return await apiRequest<DiagramValidationResult>(`diagrams/${id}/validate`, {
      method: 'POST',
    });
  },

  async getSuggestions(id: string): Promise<RelationSuggestion[]> {
    ensureDevSession();
    return await apiRequest<RelationSuggestion[]>(`diagrams/${id}/suggestions`);
  },

  async deleteDiagram(id: string): Promise<void> {
    ensureDevSession();
    await apiRequest<void>(`diagrams/${id}`, {
      method: 'DELETE',
    });
  },

  async getReviews(projectId: string, status?: string, page = 1, pageSize = 20): Promise<PagedResult<Review>> {
    ensureDevSession();
    const query = new URLSearchParams({ projectId, page: page.toString(), pageSize: pageSize.toString() });
    if (status) query.set('status', status);
    return await apiRequest<PagedResult<Review>>(`reviews?${query.toString()}`);
  },

  async submitReview(projectId: string): Promise<Review> {
    ensureDevSession();
    return await apiRequest<Review>('reviews', {
      method: 'POST',
      body: JSON.stringify({ projectId }),
    });
  },

  async approveReview(reviewId: string, note?: string): Promise<Review> {
    ensureDevSession();
    return await apiRequest<Review>(`reviews/${reviewId}/approve`, {
      method: 'POST',
      body: JSON.stringify({ note }),
    });
  },

  async requestChangesReview(reviewId: string, note: string): Promise<Review> {
    ensureDevSession();
    return await apiRequest<Review>(`reviews/${reviewId}/request-changes`, {
      method: 'POST',
      body: JSON.stringify({ note }),
    });
  },

  async getComments(diagramId: string): Promise<Comment[]> {
    ensureDevSession();
    return await apiRequest<Comment[]>(`diagrams/${diagramId}/comments`);
  },

  async addComment(diagramId: string, content: string): Promise<Comment> {
    ensureDevSession();
    return await apiRequest<Comment>(`diagrams/${diagramId}/comments`, {
      method: 'POST',
      body: JSON.stringify({ content }),
    });
  },

  exportZipUrl(projectId: string): string {
    return `/api/diagrams/export?projectId=${projectId}`;
  },
};
