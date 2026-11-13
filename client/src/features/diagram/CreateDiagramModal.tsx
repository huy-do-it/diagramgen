import { useEffect, useId, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { Modal } from '../../shared/Modal';
import { ApiError, flattenErrors } from '../../shared/api';
import { useToast } from '../../shared/Toast';
import { diagramApi } from './diagramApi';
import { DiagramType, DiagramTypeLabels } from './types';
import type { DiagramDetail } from './types';

const example = JSON.stringify({
  systemBoundary: 'CampusEats',
  actors: ['Student', 'Restaurant'],
  useCases: ['Order Food', 'Track Order', 'Update Menu'],
  relations: [
    { from: 'Student', to: 'Order Food', type: 'association' },
    { from: 'Student', to: 'Track Order', type: 'association' },
    { from: 'Restaurant', to: 'Update Menu', type: 'association' },
  ],
}, null, 2);

export function CreateDiagramModal({ projectId, onClose, onCreated }: {
  projectId: string;
  onClose: () => void;
  onCreated: (diagram: DiagramDetail) => void;
}) {
  const id = useId();
  const notify = useToast();
  const mounted = useRef(false);
  const pending = useRef(false);
  const errorElement = useRef<HTMLParagraphElement>(null);
  const [title, setTitle] = useState('CampusEats Order Management');
  const [type, setType] = useState<DiagramType>(DiagramType.UseCase);
  const [modelJson, setModelJson] = useState(example);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [errorField, setErrorField] = useState<'title' | 'model' | null>(null);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  useEffect(() => { if (error) errorElement.current?.focus(); }, [error]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (pending.current) return;
    setError(null);
    setErrorField(null);
    if (!title.trim()) {
      setErrorField('title');
      setError('Nhập tiêu đề sơ đồ.');
      return;
    }
    try { JSON.parse(modelJson); } catch {
      setErrorField('model');
      setError('Model JSON chưa đúng cú pháp. Kiểm tra dấu ngoặc, dấu phẩy và dấu nháy.');
      return;
    }
    pending.current = true;
    setBusy(true);
    try {
      const created = await diagramApi.createDiagram({ projectId, type, title: title.trim(), modelJson });
      if (!mounted.current) return;
      notify('Đã tạo sơ đồ.');
      onClose();
      onCreated(created);
    } catch (error: unknown) {
      if (!mounted.current) return;
      setError(error instanceof ApiError
        ? [error.message, ...flattenErrors(error)].join(' ')
        : error instanceof Error ? error.message : 'Không tạo được sơ đồ. Vui lòng thử lại.');
    } finally {
      pending.current = false;
      if (mounted.current) setBusy(false);
    }
  }

  return <Modal title="Tạo sơ đồ UML mới" onClose={onClose} busy={busy}>
    <form onSubmit={submit} aria-busy={busy} aria-describedby={error ? `${id}-error` : undefined}>
      {error && <p ref={errorElement} id={`${id}-error`} className="notice danger" role="alert" tabIndex={-1}>{error}</p>}
      <fieldset disabled={busy}>
        <label htmlFor={`${id}-title`}>Tiêu đề sơ đồ</label>
        <input id={`${id}-title`} data-autofocus required value={title} onChange={event => setTitle(event.target.value)}
          aria-invalid={errorField === 'title'} aria-describedby={error ? `${id}-error` : undefined} />
        <label htmlFor={`${id}-type`}>Loại sơ đồ</label>
        <select id={`${id}-type`} className="input" value={type} onChange={event => setType(Number(event.target.value))}>
          {Object.entries(DiagramTypeLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
        </select>
        <label htmlFor={`${id}-model`}>Model JSON</label>
        <textarea id={`${id}-model`} className="code-textarea" rows={8} required value={modelJson} onChange={event => setModelJson(event.target.value)}
          aria-invalid={errorField === 'model'} aria-describedby={error ? `${id}-error` : undefined} />
        <button type="button" className="dialog-example" onClick={() => {
          setTitle('Sơ đồ mẫu cần kiểm tra UML');
          setModelJson(JSON.stringify({ systemBoundary: 'CampusEats', actors: ['Student', 'Admin'], useCases: ['Login'],
            relations: [{ from: 'Student', to: 'Admin', type: 'association' }] }, null, 2));
        }}>Nạp ví dụ cần kiểm tra UML</button>
      </fieldset>
      <div className="dialog-actions">
        <button type="button" className="button button-secondary" disabled={busy} onClick={onClose}>Hủy</button>
        <button type="submit" className="button button-primary" disabled={busy}>{busy ? 'Đang tạo…' : 'Tạo sơ đồ'}</button>
      </div>
    </form>
  </Modal>;
}
