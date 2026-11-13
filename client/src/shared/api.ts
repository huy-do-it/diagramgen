import { clearSession, readSession, saveSession, getSessionVersion } from '../auth/session';
import type { Session } from '../auth/session';

/** Danh sách phân trang (TASKS §1.3). */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

/** Lỗi trả về từ mọi service (RFC 7807 + code/errors). */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  errors?: Record<string, string[]>;
  traceId?: string;
  correlationId?: string;
}

export class ApiError extends Error {
  readonly status: number;
  readonly code?: string;
  readonly errors: Record<string, string[]>;

  constructor(status: number, message: string, code?: string, errors: Record<string, string[]> = {}) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
    this.errors = errors;
  }
}

const API_BASE = '/api';
const REQUEST_TIMEOUT_MS = 15_000;
const SESSION_EXPIRED = 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.';

const statusMessages: Record<number, string> = {
  401: SESSION_EXPIRED,
  403: 'Bạn không có quyền thực hiện thao tác này.',
  404: 'Không tìm thấy dữ liệu.',
  409: 'Dữ liệu đã bị người khác thay đổi hoặc đang được xử lý. Tải lại rồi thử lại.',
  503: 'Dịch vụ tạm thời không phản hồi. Vui lòng thử lại sau.',
};

let unauthorizedHandler: (() => void) | undefined;

/** AuthProvider đăng ký 1 handler duy nhất để đăng xuất khi gặp 401. */
export function setUnauthorizedHandler(handler: (() => void) | undefined) {
  unauthorizedHandler = handler;
}

export type RequestOptions = RequestInit & { authenticated?: boolean };

// Each login/logout starts a new generation; token rotation stays in that generation.
let refreshFlight: { version: number; promise: Promise<Session | null> } | null = null;

export function getOrQueueRefreshSession(): Promise<Session | null> {
  const original = readSession();
  const version = getSessionVersion();
  if (!original?.refreshToken) return Promise.resolve(null);
  if (refreshFlight?.version === version) return refreshFlight.promise;

  const flight = { version, promise: Promise.resolve<Session | null>(null) };
  flight.promise = (async () => {
    try {
      const response = await fetch(`${API_BASE}/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: original.refreshToken }),
        credentials: 'omit',
        signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
      });
      if (!response.ok) return null;
      const data = await response.json();
      if (getSessionVersion() !== version || readSession()?.refreshToken !== original.refreshToken) return null;
      if (typeof data.accessToken !== 'string' || typeof data.refreshToken !== 'string' || !data.refreshToken) return null;
      return saveSession(data.accessToken, data.refreshToken, data.expiresIn, true);
    } catch {
      return null;
    }
  })().finally(() => {
    if (refreshFlight === flight) refreshFlight = null;
  });
  refreshFlight = flight;
  return flight.promise;
}

export const refreshAuthSession = getOrQueueRefreshSession;

/** Refresh at most once after a 401, preserving the request's original session. */
export async function apiFetch(path: string, options: RequestOptions = {}): Promise<Response> {
  const { authenticated = true, ...init } = options;
  const cleanPath = path.replace(/^\/+/, '');
  const mayRefresh = authenticated && cleanPath !== 'auth/refresh' && cleanPath !== 'auth/login';
  const version = getSessionVersion();
  let session = readSession();
  const checkSession = () => {
    init.signal?.throwIfAborted();
    if (authenticated && getSessionVersion() !== version) throw new ApiError(401, SESSION_EXPIRED);
  };
  const expireSession = () => {
    if (getSessionVersion() === version && readSession()?.accessToken === session?.accessToken) {
      clearSession();
      unauthorizedHandler?.();
    }
  };

  if (mayRefresh && session && session.expiresAt <= Date.now()) {
    const refreshed = await getOrQueueRefreshSession();
    checkSession();
    if (!refreshed) {
      expireSession();
      throw new ApiError(401, SESSION_EXPIRED);
    }
    session = refreshed;
  }
  if (authenticated && !session) {
    expireSession();
    throw new ApiError(401, SESSION_EXPIRED);
  }

  for (let attempt = 0; attempt < 2; attempt++) {
    checkSession();
    const headers = new Headers(init.headers);
    if (authenticated && session) headers.set('Authorization', `Bearer ${session.accessToken}`);
    if (init.body && !(init.body instanceof FormData) && !headers.has('Content-Type')) {
      headers.set('Content-Type', 'application/json');
    }
    const timeout = AbortSignal.timeout(REQUEST_TIMEOUT_MS);
    const signal = init.signal ? AbortSignal.any([init.signal, timeout]) : timeout;
    let response: Response;
    try {
      response = await fetch(`${API_BASE}/${cleanPath}`, { ...init, headers, signal, credentials: 'omit' });
    } catch (error) {
      if (init.signal?.aborted) throw error;
      throw new ApiError(0, timeout.aborted
        ? 'Yêu cầu quá thời gian chờ. Vui lòng thử lại.'
        : 'Không kết nối được tới máy chủ. Kiểm tra mạng rồi thử lại.');
    }
    checkSession();
    if (response.status === 401 && mayRefresh && attempt === 0) {
      // Another request may already have rotated this session's token.
      const current = readSession();
      const refreshed = current && current.accessToken !== session?.accessToken
        ? current : await getOrQueueRefreshSession();
      checkSession();
      if (refreshed) {
        session = refreshed;
        continue;
      }
      expireSession();
    } else if (response.status === 401 && authenticated) {
      expireSession();
    }
    if (response.ok) return response;
    const problem: ProblemDetails | null = await response.json().catch(() => null);
    throw new ApiError(response.status,
      problem?.detail || statusMessages[response.status] || problem?.title || 'Không thực hiện được yêu cầu. Vui lòng thử lại.',
      problem?.code, problem?.errors ?? {});
  }
  throw new ApiError(401, SESSION_EXPIRED);
}

/** Gọi API và đọc body JSON. 204 No Content → <c>undefined</c>. */
export async function apiRequest<T>(path: string, options?: RequestOptions): Promise<T> {
  const response = await apiFetch(path, options);
  if (response.status === 204) return undefined as T;
  try {
    return await response.json() as T;
  } catch (error) {
    if (options?.signal?.aborted) throw error;
    throw new ApiError(response.status, 'Máy chủ trả về dữ liệu không hợp lệ.');
  }
}

/** Gộp lỗi validate thành danh sách dòng để hiển thị dưới form. */
export function flattenErrors(error: ApiError): string[] {
  return Object.entries(error.errors).flatMap(([field, messages]) => messages.map(message => `${field}: ${message}`));
}
