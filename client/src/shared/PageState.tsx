import { ApiError } from './api';

export function requestErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof ApiError) {
    if (error.status === 403) return 'Bạn không có quyền thực hiện thao tác này.';
    if (error.status === 409 || error.status === 412) return `${error.message} Tải lại dữ liệu rồi thử lại.`;
    return error.message;
  }
  return fallback;
}

export function LoadingState({ label }: { label: string }) {
  return <div className="page-loading" role="status" aria-live="polite" aria-busy="true">
    <span className="sr-only">{label}</span>
    <div aria-hidden="true"><div className="skeleton-line" /><div className="skeleton-line" /><div className="skeleton-line" /></div>
  </div>;
}

export function LoadError({ message, onRetry }: { message: string; onRetry: () => void }) {
  return <div className="notice danger" role="alert"><div>{message}</div>
    <button type="button" className="button button-secondary" onClick={onRetry}>Thử lại</button>
  </div>;
}
