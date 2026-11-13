export interface Session {
  accessToken: string;
  refreshToken: string;
  expiresAt: number;
}

const SESSION_KEY = 'umlforge.session.v2';
let sessionVersion = 0;
const listeners = new Set<(session: Session | null) => void>();

export const getSessionVersion = () => sessionVersion;
export function subscribeSession(listener: (session: Session | null) => void) {
  listeners.add(listener);
  return () => { listeners.delete(listener); };
}
function notifySession(session: Session | null) {
  listeners.forEach(listener => listener(session));
}

export function readSession(): Session | null {
  try {
    const value = sessionStorage.getItem(SESSION_KEY);
    if (!value) return null;
    const session: unknown = JSON.parse(value);
    if (typeof session !== 'object' || session === null) return null;
    const candidate = session as Partial<Session>;
    return typeof candidate.accessToken === 'string' && candidate.accessToken.length > 0
      && typeof candidate.refreshToken === 'string'
      && typeof candidate.expiresAt === 'number' && Number.isFinite(candidate.expiresAt)
      ? candidate as Session : null;
  } catch {
    return null;
  }
}

export function saveSession(accessToken: string, refreshToken: string, expiresIn: number, isRefresh = false): Session {
  let expiresAt: number;
  try {
    // exp chỉ dùng để báo hết phiên phía client; API vẫn là nơi kiểm tra JWT
    const payload = JSON.parse(atob(accessToken.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
    if (!Number.isFinite(payload.exp) || !Number.isFinite(expiresIn) || expiresIn <= 0) throw new Error();
    expiresAt = Math.min(payload.exp * 1000, Date.now() + expiresIn * 1000);
    if (expiresAt <= Date.now()) throw new Error();
  } catch {
    throw new Error('Máy chủ trả về phiên đăng nhập không hợp lệ hoặc đã hết hạn. Vui lòng đăng nhập lại.');
  }
  const session = { accessToken, refreshToken, expiresAt };
  try {
    sessionStorage.setItem(SESSION_KEY, JSON.stringify(session));
  } catch {
    throw new Error('Trình duyệt không lưu được phiên đăng nhập. Hãy cho phép lưu trữ dữ liệu trang rồi thử lại.');
  }
  if (!isRefresh) sessionVersion++;
  notifySession(session);
  return session;
}

export function clearSession() {
  sessionVersion++;
  try { sessionStorage.removeItem(SESSION_KEY); } catch { /* Storage có thể bị tắt. */ }
  notifySession(null);
}
