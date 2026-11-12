import { apiFetch, apiRequest } from '../../../shared/api';
import type { RenderSnapshot, RenderStatus } from '../../../shared/useRenderStatus';
import { DiagramTypeEnum } from '../templates/diagramTemplates';

/** Tên loại sơ đồ trong JSON của API (TASKS §1.3 — enum dạng chuỗi). */
export type DiagramTypeName = 'BusinessContext' | 'UseCase' | 'Sequence' | 'Activity' | 'State';

const TYPE_NAMES: Record<DiagramTypeEnum, DiagramTypeName> = {
  [DiagramTypeEnum.BusinessContext]: 'BusinessContext',
  [DiagramTypeEnum.UseCase]: 'UseCase',
  [DiagramTypeEnum.Sequence]: 'Sequence',
  [DiagramTypeEnum.Activity]: 'Activity',
  [DiagramTypeEnum.State]: 'State',
};

export function toTypeName(type: DiagramTypeEnum): DiagramTypeName {
  return TYPE_NAMES[type];
}

/** Nhận cả tên ("UseCase") lẫn số (2) để không vỡ nếu API trả số. */
export function toTypeEnum(type: DiagramTypeName | number): DiagramTypeEnum {
  if (typeof type === 'number') return type as DiagramTypeEnum;
  const entry = Object.entries(TYPE_NAMES).find(([, name]) => name === type);
  return entry ? Number(entry[0]) as DiagramTypeEnum : DiagramTypeEnum.UseCase;
}

/** GET /api/diagrams/{id} (T-01, T-02). */
export interface DiagramDetail {
  id: string;
  projectId: string;
  type: DiagramTypeName;
  title: string;
  modelJson: string;
  status: RenderStatus;
  currentVersion: number;
  svgXml?: string | null;
  plantUml?: string | null;
  renderedAt?: string | null;
  lastError?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

/** Response của POST/PUT: thêm cảnh báo UML (T-05) và cờ cần gửi duyệt lại (BR-31). */
export interface DiagramSaveResponse extends DiagramDetail {
  warnings?: string[];
  needsResubmit?: boolean;
}

export interface DiagramWithEtag<T extends DiagramDetail = DiagramDetail> {
  diagram: T;
  etag: string | null;
}

export interface ValidationResult {
  isValid: boolean;
  errors: string[];
  warnings: string[];
}

export interface RelationSuggestion {
  from: string;
  to: string;
  description: string;
}

export interface RenderAccepted {
  diagramId: string;
  version: number;
  status: RenderStatus;
}

function diagramPath(id: string, suffix = '') {
  return `/diagrams/${encodeURIComponent(id)}${suffix}`;
}

async function readWithEtag<T extends DiagramDetail>(response: Response): Promise<DiagramWithEtag<T>> {
  return { diagram: await response.json() as T, etag: response.headers.get('ETag') };
}

export async function getDiagram(id: string, signal?: AbortSignal): Promise<DiagramWithEtag> {
  return readWithEtag(await apiFetch(diagramPath(id), { signal }));
}

export async function createDiagram(
  request: { projectId: string; type: DiagramTypeName; title: string; modelJson: string },
): Promise<DiagramWithEtag<DiagramSaveResponse>> {
  return readWithEtag(await apiFetch('/diagrams', { method: 'POST', body: JSON.stringify(request) }));
}

/** PUT bắt buộc If-Match = ETag lần đọc trước; sai → 409 BR-16 (T-02). */
export async function updateDiagram(
  id: string, request: { title: string; modelJson: string }, etag: string | null,
): Promise<DiagramWithEtag<DiagramSaveResponse>> {
  const headers: Record<string, string> = etag ? { 'If-Match': etag } : {};
  return readWithEtag(await apiFetch(diagramPath(id), { method: 'PUT', headers, body: JSON.stringify(request) }));
}

export function deleteDiagram(id: string): Promise<void> {
  return apiRequest<void>(diagramPath(id), { method: 'DELETE' });
}

/** Kiểm tra luật UML qua gRPC, không lưu (T-05). */
export function validateDiagram(id: string, modelJson: string): Promise<ValidationResult> {
  return apiRequest<ValidationResult>(diagramPath(id, '/validate'), {
    method: 'POST', body: JSON.stringify({ modelJson }),
  });
}

/** Gợi ý quan hệ còn thiếu (T-05); chấp nhận cả mảng lẫn { suggestions: [...] }. */
export async function getSuggestions(id: string): Promise<RelationSuggestion[]> {
  const body = await apiRequest<RelationSuggestion[] | { suggestions?: RelationSuggestion[] }>(diagramPath(id, '/suggestions'));
  return Array.isArray(body) ? body : body.suggestions ?? [];
}

/** Gửi yêu cầu render qua Outbox → 202 (T-03); đang Pending/Rendering → 409 BR-14. */
export function requestRender(id: string): Promise<RenderAccepted> {
  return apiRequest<RenderAccepted>(diagramPath(id, '/render'), { method: 'POST' });
}

/** Ảnh chụp trạng thái render ban đầu cho useRenderStatus. */
export function toRenderSnapshot(diagram: DiagramDetail, etag: string | null): RenderSnapshot {
  return {
    status: diagram.status,
    svg: diagram.svgXml ?? null,
    plantUml: diagram.plantUml ?? null,
    error: diagram.lastError ?? null,
    version: diagram.currentVersion,
    renderedAt: diagram.renderedAt ?? null,
    etag,
  };
}
