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

const API_BASE = '/api';

export const DEV_TOKENS = {
  Leader: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMTExMTExMS0xMTExLTExMTEtMTExMS0xMTExMTExMTExMTEiLCJ1bmlxdWVfbmFtZSI6ImxlYWRlcl9kZXYiLCJyb2xlIjoiTGVhZGVyIiwiaXNzIjoidW1sZm9yZ2UiLCJhdWQiOiJ1bWxmb3JnZS1jbGllbnQiLCJleHAiOjIwNTAwMDAwMDB9.6F67ss7Gn-a-9DIynnold2dZ0sjSluvuZvm2A-nbNFg',
  Lecturer: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIyMjIyMjIyMi0yMjIyLTIyMjItMjIyMi0yMjIyMjIyMjIyMjIiLCJ1bmlxdWVfbmFtZSI6ImxlY3R1cmVyX2RldiIsInJvbGUiOiJMZWN0dXJlciIsImlzcyI6InVtbGZvcmdlIiwiYXVkIjoidW1sZm9yZ2UtY2xpZW50IiwiZXhwIjoyMDUwMDAwMDAwfQ.wqZeNzFbk7sxZhvSjMGYspGwvZmoXRvkezmO5NCrFuM',
  Member: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIzMzMzMzMzMy0zMzMzLTMzMzMtMzMzMy0zMzMzMzMzMzMzMzMiLCJ1bmlxdWVfbmFtZSI6Im1lbWJlcl9kZXYiLCJyb2xlIjoiU3R1ZGVudCIsImlzcyI6InVtbGZvcmdlIiwiYXVkIjoidW1sZm9yZ2UtY2xpZW50IiwiZXhwIjoyMDUwMDAwMDAwfQ.yV409fpXptrDCoo4dF2kdViWTOkJsGpu3MATWyFzCU4'
};

function getAuthHeaders(): HeadersInit {
  const token = localStorage.getItem('token') || DEV_TOKENS.Leader;
  return {
    'Content-Type': 'application/json',
    Authorization: `Bearer ${token}`,
  };
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
    const query = new URLSearchParams();
    query.set('projectId', params.projectId);
    if (params.type) query.set('type', params.type.toString());
    if (params.status) query.set('status', params.status);
    if (params.search) query.set('search', params.search);
    if (params.sort) query.set('sort', params.sort);
    if (params.page) query.set('page', params.page.toString());
    if (params.pageSize) query.set('pageSize', params.pageSize.toString());

    const res = await fetch(`${API_BASE}/diagrams?${query.toString()}`, {
      headers: getAuthHeaders(),
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },

  async getDiagramById(id: string): Promise<{ diagram: DiagramDetail; etag: string | null }> {
    const res = await fetch(`${API_BASE}/diagrams/${id}`, {
      headers: getAuthHeaders(),
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
    const res = await fetch(`${API_BASE}/diagrams`, {
      method: 'POST',
      headers: getAuthHeaders(),
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
      ...(getAuthHeaders() as Record<string, string>),
      ...(ifMatch ? { 'If-Match': ifMatch } : {}),
    };
    const res = await fetch(`${API_BASE}/diagrams/${id}`, {
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
    const res = await fetch(`${API_BASE}/diagrams/${id}/render`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },

  async getVersions(
    id: string,
    page = 1,
    pageSize = 20
  ): Promise<PagedResult<DiagramVersionSummary>> {
    const res = await fetch(`${API_BASE}/diagrams/${id}/versions?page=${page}&pageSize=${pageSize}`, {
      headers: getAuthHeaders(),
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },

  async getVersionDetail(id: string, version: number): Promise<DiagramVersionDetail> {
    const res = await fetch(`${API_BASE}/diagrams/${id}/versions/${version}`, {
      headers: getAuthHeaders(),
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },

  async validateDiagram(id: string): Promise<DiagramValidationResult> {
    const res = await fetch(`${API_BASE}/diagrams/${id}/validate`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },

  async getSuggestions(id: string): Promise<RelationSuggestion[]> {
    const res = await fetch(`${API_BASE}/diagrams/${id}/suggestions`, {
      headers: getAuthHeaders(),
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },

  async deleteDiagram(id: string): Promise<void> {
    const res = await fetch(`${API_BASE}/diagrams/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });
    if (!res.ok) throw new Error(await res.text());
  },

  async getReviews(projectId: string, status?: string, page = 1, pageSize = 20): Promise<PagedResult<Review>> {
    const query = new URLSearchParams({ projectId, page: page.toString(), pageSize: pageSize.toString() });
    if (status) query.set('status', status);
    const res = await fetch(`${API_BASE}/reviews?${query.toString()}`, {
      headers: getAuthHeaders(),
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },

  async submitReview(projectId: string): Promise<Review> {
    const res = await fetch(`${API_BASE}/reviews`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ projectId }),
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },

  async approveReview(reviewId: string, note?: string): Promise<Review> {
    const res = await fetch(`${API_BASE}/reviews/${reviewId}/approve`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ note }),
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },

  async requestChangesReview(reviewId: string, note: string): Promise<Review> {
    const res = await fetch(`${API_BASE}/reviews/${reviewId}/request-changes`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ note }),
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },

  async getComments(diagramId: string): Promise<Comment[]> {
    const res = await fetch(`${API_BASE}/diagrams/${diagramId}/comments`, {
      headers: getAuthHeaders(),
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },

  async addComment(diagramId: string, content: string): Promise<Comment> {
    const res = await fetch(`${API_BASE}/diagrams/${diagramId}/comments`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ content }),
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  },

  exportZipUrl(projectId: string): string {
    return `${API_BASE}/diagrams/export?projectId=${projectId}`;
  },
};
