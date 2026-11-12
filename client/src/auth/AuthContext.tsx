import { createContext, useCallback, useContext, useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { ApiError, apiRequest, setUnauthorizedHandler } from '../shared/api';
import { clearSession, readSession, saveSession } from './session';
import type { Session } from './session';

export type UserRole = 'Student' | 'Lecturer' | 'Admin';

export interface User {
  id: string;
  username: string;
  fullName: string;
  role: UserRole;
}

/** Response của POST /api/auth/login (TASKS H-01). */
export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  user: User;
}

interface AuthState {
  user: User | null;
  loading: boolean;
  notice: string | null;
  signIn: (username: string, password: string, signal?: AbortSignal) => Promise<void>;
  signOut: () => void;
}

const AuthContext = createContext<AuthState | null>(null);
const expiredMessage = 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [session, setSession] = useState<Session | null>(null);
  const [loading, setLoading] = useState(true);
  const [notice, setNotice] = useState<string | null>(null);

  const endSession = useCallback((message: string | null = null) => {
    clearSession();
    setSession(null);
    setUser(null);
    setNotice(message);
    setLoading(false);
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    setUnauthorizedHandler(() => endSession(expiredMessage));
    const saved = readSession();
    if (!saved) {
      clearSession();
      setLoading(false);
    } else if (saved.expiresAt <= Date.now()) {
      endSession(expiredMessage);
    } else {
      apiRequest<User>('/users/me', { signal: controller.signal }).then(data => {
        if (!controller.signal.aborted && readSession()?.accessToken === saved.accessToken) {
          setUser(data);
          setSession(saved);
          setLoading(false);
        }
      }).catch((error: unknown) => {
        const current = readSession();
        if (!controller.signal.aborted && (!current || current.accessToken === saved.accessToken)) {
          endSession(error instanceof ApiError && error.status === 401 ? expiredMessage
            : 'Không kiểm tra được phiên đăng nhập. Kiểm tra mạng rồi đăng nhập lại.');
        }
      });
    }
    return () => { controller.abort(); setUnauthorizedHandler(undefined); };
  }, [endSession]);

  useEffect(() => {
    if (!session) return;
    const expire = () => {
      const current = readSession();
      if (!current || current.accessToken === session.accessToken) endSession(expiredMessage);
    };
    const checkExpiry = () => { if (Date.now() >= session.expiresAt) expire(); };
    const timer = window.setTimeout(expire, Math.max(0, session.expiresAt - Date.now()));
    window.addEventListener('focus', checkExpiry);
    document.addEventListener('visibilitychange', checkExpiry);
    return () => {
      window.clearTimeout(timer);
      window.removeEventListener('focus', checkExpiry);
      document.removeEventListener('visibilitychange', checkExpiry);
    };
  }, [session, endSession]);

  async function signIn(username: string, password: string, signal?: AbortSignal) {
    const data = await apiRequest<LoginResponse>('/auth/login', {
      method: 'POST', body: JSON.stringify({ username: username.trim(), password }), authenticated: false, signal,
    });
    signal?.throwIfAborted();
    const saved = saveSession(data.accessToken, data.refreshToken, data.expiresIn);
    setSession(saved);
    setUser(data.user);
    setNotice(null);
  }

  return <AuthContext.Provider value={{ user, loading, notice, signIn, signOut: () => endSession() }}>
    {children}
  </AuthContext.Provider>;
}

export function useAuth() {
  const auth = useContext(AuthContext);
  if (!auth) throw new Error('useAuth phải được dùng bên trong AuthProvider.');
  return auth;
}
