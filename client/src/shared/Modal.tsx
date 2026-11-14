import { useEffect, useId, useRef } from 'react';
import type { ReactNode } from 'react';
import { createPortal } from 'react-dom';
import './feedback.css';

export function Modal({ title, children, onClose, busy = false, className = '' }: {
  title: string;
  children: ReactNode;
  onClose: () => void;
  busy?: boolean;
  className?: string;
}) {
  const dialog = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  useEffect(() => {
    const element = dialog.current!;
    const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    element.showModal();
    element.querySelector<HTMLElement>('[data-autofocus]')?.focus();
    return () => {
      element.close();
      if (opener?.isConnected) opener.focus();
    };
  }, []);
  useEffect(() => { if (busy) dialog.current?.focus(); }, [busy]);

  return createPortal(<dialog ref={dialog} className={`modal feedback-dialog ${className}`} tabIndex={-1} aria-modal="true" aria-labelledby={titleId}
    onKeyDown={event => {
      if (event.key === 'Escape') {
        event.preventDefault();
        event.stopPropagation();
        if (!busy) onClose();
        return;
      }
      if (event.key !== 'Tab') return;
      const items = [...event.currentTarget.querySelectorAll<HTMLElement>(
        'button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), a[href], [tabindex]:not([tabindex="-1"])',
      )].filter(element => element.getClientRects().length > 0);
      const first = items[0];
      const last = items.at(-1);
      if (!first) {
        event.preventDefault();
        event.currentTarget.focus();
      } else if (event.shiftKey && (document.activeElement === first || document.activeElement === event.currentTarget)) {
        event.preventDefault();
        last?.focus();
      } else if (!event.shiftKey && (document.activeElement === last || document.activeElement === event.currentTarget)) {
        event.preventDefault();
        first.focus();
      }
    }}
    onCancel={event => { event.preventDefault(); if (!busy) onClose(); }}>
    <div className="modal-header">
      <h2 id={titleId}>{title}</h2>
      <button type="button" className="button button-ghost button-small" aria-label="Đóng hộp thoại" disabled={busy} onClick={onClose}>×</button>
    </div>
    {children}
  </dialog>, document.body);
}
