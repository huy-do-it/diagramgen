import { createContext, useCallback, useContext, useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import './feedback.css';

type ToastKind = 'success' | 'error' | 'info';
type Notify = (message: string, kind?: ToastKind) => void;
const ToastContext = createContext<Notify | null>(null);

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toast, setToast] = useState<{ message: string; kind: ToastKind; id: number } | null>(null);
  const notify = useCallback<Notify>((message, kind = 'success') => {
    setToast(previous => ({ message, kind, id: (previous?.id ?? 0) + 1 }));
  }, []);
  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(null), 4000);
    return () => window.clearTimeout(timer);
  }, [toast]);

  return <ToastContext.Provider value={notify}>
    {children}
    <div className="toast-region" role="status" aria-live="polite" aria-atomic="true">
      {toast && <div className={`toast toast--${toast.kind}`} key={toast.id}>
        <span>{toast.message}</span>
        <button type="button" className="toast-close" aria-label="Đóng thông báo" onClick={() => setToast(null)}>×</button>
      </div>}
    </div>
  </ToastContext.Provider>;
}

export function useToast() {
  const notify = useContext(ToastContext);
  if (!notify) throw new Error('useToast must be used within ToastProvider.');
  return notify;
}
