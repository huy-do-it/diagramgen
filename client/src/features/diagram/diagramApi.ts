import { apiFetch, apiRequest } from '../../shared/api';
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
    const res = await apiFetch(`diagrams/${encodeURIComponent(id)}`);
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
    const headers: Record<string, string> = ifMatch ? { 'If-Match': ifMatch } : {};
    return await apiRequest<DiagramDetail>(`diagrams/${encodeURIComponent(id)}`, {
      method: 'PUT',
      headers,
      body: JSON.stringify(data),
    });
  },

  async requestRender(id: string): Promise<RenderResponse> {
    return await apiRequest<RenderResponse>(`diagrams/${encodeURIComponent(id)}/render`, {
      method: 'POST',
    });
  },

  async getVersions(
    id: string,
    page = 1,
    pageSize = 20
  ): Promise<PagedResult<DiagramVersionSummary>> {
    return await apiRequest<PagedResult<DiagramVersionSummary>>(
      `diagrams/${encodeURIComponent(id)}/versions?page=${page}&pageSize=${pageSize}`
    );
  },

  async getVersionDetail(id: string, version: number): Promise<DiagramVersionDetail> {
    return await apiRequest<DiagramVersionDetail>(
      `diagrams/${encodeURIComponent(id)}/versions/${version}`
    );
  },

  async restoreVersion(id: string, version: number): Promise<DiagramDetail> {
    return await apiRequest<DiagramDetail>(
      `diagrams/${encodeURIComponent(id)}/versions/${version}/restore`,
      {
        method: 'POST',
      }
    );
  },

  async validateDiagram(id: string): Promise<DiagramValidationResult> {
    return await apiRequest<DiagramValidationResult>(
      `diagrams/${encodeURIComponent(id)}/validate`,
      {
        method: 'POST',
      }
    );
  },

  async getSuggestions(id: string): Promise<RelationSuggestion[]> {
    return await apiRequest<RelationSuggestion[]>(
      `diagrams/${encodeURIComponent(id)}/suggestions`
    );
  },

  async deleteDiagram(id: string): Promise<void> {
    await apiRequest<void>(`diagrams/${encodeURIComponent(id)}`, {
      method: 'DELETE',
    });
  },

  async getReviews(projectId: string, status?: string, page = 1, pageSize = 20): Promise<PagedResult<Review>> {
    const query = new URLSearchParams({ projectId, page: page.toString(), pageSize: pageSize.toString() });
    if (status) query.set('status', status);
    return await apiRequest<PagedResult<Review>>(`reviews?${query.toString()}`);
  },

  async submitReview(projectId: string): Promise<Review> {
    return await apiRequest<Review>('reviews', {
      method: 'POST',
      body: JSON.stringify({ projectId }),
    });
  },

  async approveReview(reviewId: string, note?: string): Promise<Review> {
    return await apiRequest<Review>(`reviews/${encodeURIComponent(reviewId)}/approve`, {
      method: 'POST',
      body: JSON.stringify({ note }),
    });
  },

  async requestChangesReview(reviewId: string, note: string): Promise<Review> {
    return await apiRequest<Review>(`reviews/${encodeURIComponent(reviewId)}/request-changes`, {
      method: 'POST',
      body: JSON.stringify({ note }),
    });
  },

  async getComments(diagramId: string): Promise<Comment[]> {
    return await apiRequest<Comment[]>(`diagrams/${encodeURIComponent(diagramId)}/comments`);
  },

  async addComment(diagramId: string, content: string): Promise<Comment> {
    return await apiRequest<Comment>(`diagrams/${encodeURIComponent(diagramId)}/comments`, {
      method: 'POST',
      body: JSON.stringify({ content }),
    });
  },

  async exportZip(projectId: string): Promise<Blob> {
    const response = await apiFetch(`/diagrams/export?projectId=${encodeURIComponent(projectId)}`);
    return response.blob();
  },
};
