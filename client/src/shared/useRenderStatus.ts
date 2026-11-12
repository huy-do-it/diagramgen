import { useCallback, useEffect, useState } from 'react';
import { ApiError, apiFetch } from './api';

export type RenderStatus = 'Draft' | 'Pending' | 'Rendering' | 'Rendered' | 'Failed';

/** Phần trạng thái render của GET /api/diagrams/{id} (TASKS T-01, T-03). */
export interface RenderSnapshot {
  status: RenderStatus;
  svg: string | null;
  plantUml: string | null;
  /** Lỗi của lần render gần nhất (Failed) — SVG cũ vẫn giữ (BR-28). */
  error: string | null;
  version: number;
  renderedAt: string | null;
  /** ETag mới nhất — consumer đổi trạng thái làm RowVersion đổi, PUT sau đó phải dùng ETag này. */
  etag: string | null;
}

const ACTIVE_STATUSES: RenderStatus[] = ['Pending', 'Rendering'];
const POLL_MS = 2_000;
const RETRY_MS = 5_000;

export function isRenderActive(status: RenderStatus | null | undefined): boolean {
  return status != null && ACTIVE_STATUSES.includes(status);
}

interface DiagramRenderFields {
  status?: RenderStatus;
  svgXml?: string | null;
  plantUml?: string | null;
  lastError?: string | null;
  currentVersion?: number;
  renderedAt?: string | null;
}

export async function fetchRenderSnapshot(diagramId: string, signal?: AbortSignal): Promise<RenderSnapshot> {
  const response = await apiFetch(`/diagrams/${encodeURIComponent(diagramId)}`, { signal });
  const body = await response.json() as DiagramRenderFields;
  return {
    status: body.status ?? 'Draft',
    svg: body.svgXml ?? null,
    plantUml: body.plantUml ?? null,
    error: body.lastError ?? null,
    version: body.currentVersion ?? 0,
    renderedAt: body.renderedAt ?? null,
    etag: response.headers.get('ETag'),
  };
}

/**
 * Theo dõi trạng thái render bằng polling (ADR-07): hỏi lại mỗi 2 s khi Pending/Rendering,
 * dừng khi Rendered/Failed. Lỗi mạng/5xx thử lại sau 5 s; 401/403/404 thì dừng.
 * Gọi <c>track()</c> ngay sau POST /render để bắt đầu theo dõi.
 */
export function useRenderStatus(diagramId: string | null, initial: RenderSnapshot | null) {
  const [snapshot, setSnapshot] = useState<RenderSnapshot | null>(initial);
  const [pollError, setPollError] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);

  // Dữ liệu mới từ trang (vừa tải / vừa lưu) thay cho snapshot cũ
  useEffect(() => {
    setSnapshot(initial);
    setPollError(null);
  }, [initial]);

  const active = diagramId !== null && isRenderActive(snapshot?.status);

  useEffect(() => {
    if (!active || diagramId === null) return;
    const controller = new AbortController();
    const timer = window.setTimeout(async () => {
      let retry = true;
      try {
        const next = await fetchRenderSnapshot(diagramId, controller.signal);
        if (controller.signal.aborted) return;
        setSnapshot(next);
        setPollError(null);
      } catch (error) {
        if (controller.signal.aborted) return;
        const status = error instanceof ApiError ? error.status : 0;
        retry = status === 0 || status >= 500;
        const message = error instanceof Error ? error.message : 'Không kiểm tra được trạng thái render.';
        setPollError(retry ? `${message} Đang thử lại…` : message);
      }
      if (retry && !controller.signal.aborted) setAttempt(value => value + 1);
    }, pollError ? RETRY_MS : POLL_MS);
    return () => {
      controller.abort();
      window.clearTimeout(timer);
    };
    // pollError chỉ quyết định độ trễ nên không nằm trong deps; attempt tăng sau mỗi lần hỏi để lên lịch lần sau
  }, [active, diagramId, attempt]);

  /** Bắt đầu theo dõi ngay sau khi gửi yêu cầu render thành công (202). */
  const track = useCallback(() => {
    setPollError(null);
    setSnapshot(previous => previous
      ? { ...previous, status: 'Pending', error: null }
      : { status: 'Pending', svg: null, plantUml: null, error: null, version: 0, renderedAt: null, etag: null });
    setAttempt(value => value + 1);
  }, []);

  /** Tải lại 1 lần (ví dụ khi người dùng bấm "Tải lại"). */
  const refresh = useCallback(async () => {
    if (diagramId === null) return;
    try {
      setSnapshot(await fetchRenderSnapshot(diagramId));
      setPollError(null);
    } catch (error) {
      setPollError(error instanceof Error ? error.message : 'Không tải được sơ đồ.');
    }
  }, [diagramId]);

  return {
    snapshot,
    status: snapshot?.status ?? null,
    svg: snapshot?.svg ?? null,
    error: snapshot?.error ?? null,
    pollError,
    isRendering: active,
    track,
    refresh,
  };
}
