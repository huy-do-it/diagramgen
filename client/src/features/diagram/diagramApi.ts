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

import { apiFetch } from '../../shared/api';

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

    const res = await apiFetch(`/diagrams?${query.toString()}`, {
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },

  async getDiagramById(id: string): Promise<{ diagram: DiagramDetail; etag: string | null }> {
    const res = await apiFetch(`/diagrams/${id}`, {
    });
    if (!res.ok) throw new Error(await res.text());
    const etag = res.headers.get('ETag');
    const diagram = await res.json();
    return { diagram, etag };
  },

  async createDiagram(data: {
    projectId: string;
    type: number;
    title: string;
    modelJson: string;
  }): Promise<DiagramDetail> {
    const res = await apiFetch(`/diagrams`, {
      method: 'POST',
      body: JSON.stringify(data),
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({ detail: res.statusText }));
      throw new Error(err.detail || JSON.stringify(err));
    }
    return res.json();
  },

  async updateDiagram(
    id: string,
    data: { title: string; modelJson: string },
    ifMatch?: string
  ): Promise<DiagramDetail> {
    const headers: Record<string, string> = {
      ...(ifMatch ? { 'If-Match': ifMatch } : {}),
    };
    const res = await apiFetch(`/diagrams/${id}`, {
      method: 'PUT',
      headers,
      body: JSON.stringify(data),
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({ detail: res.statusText }));
      throw new Error(err.detail || JSON.stringify(err));
    }
    return res.json();
  },

  async requestRender(id: string): Promise<RenderResponse> {
    const res = await apiFetch(`/diagrams/${id}/render`, {
      method: 'POST',
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },

  async getVersions(
    id: string,
    page = 1,
    pageSize = 20
  ): Promise<PagedResult<DiagramVersionSummary>> {
    const res = await apiFetch(`/diagrams/${id}/versions?page=${page}&pageSize=${pageSize}`, {
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },

  async getVersionDetail(id: string, version: number): Promise<DiagramVersionDetail> {
    const res = await apiFetch(`/diagrams/${id}/versions/${version}`, {
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },

  async restoreVersion(id: string, version: number): Promise<DiagramDetail> {
    const res = await apiFetch(`/diagrams/${id}/versions/${version}/restore`, {
      method: 'POST',
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },

  async validateDiagram(id: string): Promise<DiagramValidationResult> {
    const res = await apiFetch(`/diagrams/${id}/validate`, {
      method: 'POST',
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },

  async getSuggestions(id: string): Promise<RelationSuggestion[]> {
    const res = await apiFetch(`/diagrams/${id}/suggestions`, {
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },

  async deleteDiagram(id: string): Promise<void> {
    const res = await apiFetch(`/diagrams/${id}`, {
      method: 'DELETE',
    });
    if (!res.ok) throw new Error(await res.text());
  },

  async getReviews(projectId: string, status?: string, page = 1, pageSize = 20): Promise<PagedResult<Review>> {
    const query = new URLSearchParams({ projectId, page: page.toString(), pageSize: pageSize.toString() });
    if (status) query.set('status', status);
    const res = await apiFetch(`/reviews?${query.toString()}`, {
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },

  async submitReview(projectId: string): Promise<Review> {
    const res = await apiFetch(`/reviews`, {
      method: 'POST',
      body: JSON.stringify({ projectId }),
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },

  async approveReview(reviewId: string, note?: string): Promise<Review> {
    const res = await apiFetch(`/reviews/${reviewId}/approve`, {
      method: 'POST',
      body: JSON.stringify({ note }),
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },

  async requestChangesReview(reviewId: string, note: string): Promise<Review> {
    const res = await apiFetch(`/reviews/${reviewId}/request-changes`, {
      method: 'POST',
      body: JSON.stringify({ note }),
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },

  async getComments(diagramId: string): Promise<Comment[]> {
    const res = await apiFetch(`/diagrams/${diagramId}/comments`, {
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },

  async addComment(diagramId: string, content: string): Promise<Comment> {
    const res = await apiFetch(`/diagrams/${diagramId}/comments`, {
      method: 'POST',
      body: JSON.stringify({ content }),
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },

  async exportZip(projectId: string): Promise<Blob> {
    const response = await apiFetch(`/diagrams/export?projectId=${encodeURIComponent(projectId)}`);
    return response.blob();
  },
};
