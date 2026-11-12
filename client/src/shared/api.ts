import { clearSession, readSession } from '../auth/session';

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

/**
 * Gọi API và trả về Response gốc (dùng khi cần header, ví dụ <c>ETag</c> để gửi lại trong <c>If-Match</c>).
 * Lỗi HTTP được đổi thành {@link ApiError} có status, code (ví dụ "BR-14") và errors.
 */
export async function apiFetch(path: string, options: RequestOptions = {}): Promise<Response> {
  const { authenticated = true, ...init } = options;
  const session = readSession();
  if (authenticated && (!session || session.expiresAt <= Date.now())) {
    clearSession();
    unauthorizedHandler?.();
    throw new ApiError(401, SESSION_EXPIRED);
  }

  const headers = new Headers(init.headers);
  if (authenticated && session) headers.set('Authorization', `Bearer ${session.accessToken}`);
  if (init.body && !(init.body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }

  const timeout = AbortSignal.timeout(REQUEST_TIMEOUT_MS);
  const signal = init.signal ? AbortSignal.any([init.signal, timeout]) : timeout;
  let response: Response;
  try {
    response = await fetch(`${API_BASE}/${path.replace(/^\/+/, '')}`, { ...init, headers, signal, credentials: 'omit' });
  } catch (error) {
    if (init.signal?.aborted) throw error;
    throw new ApiError(0, timeout.aborted
      ? 'Yêu cầu quá thời gian chờ. Vui lòng thử lại.'
      : 'Không kết nối được tới máy chủ. Kiểm tra mạng rồi thử lại.');
  }

  if (response.ok) return response;

  // 401 của request cũ không được xóa phiên đăng nhập mới hơn
  if (response.status === 401 && authenticated && readSession()?.accessToken === session?.accessToken) {
    clearSession();
    unauthorizedHandler?.();
  }
  const problem: ProblemDetails | null = await response.json().catch(() => null);
  throw new ApiError(
    response.status,
    problem?.detail || statusMessages[response.status] || problem?.title || 'Không thực hiện được yêu cầu. Vui lòng thử lại.',
    problem?.code,
    problem?.errors ?? {},
  );
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
