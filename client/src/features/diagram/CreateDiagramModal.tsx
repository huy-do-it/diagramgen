import { useEffect, useId, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { Modal } from '../../shared/Modal';
import { ApiError, flattenErrors } from '../../shared/api';
import { useToast } from '../../shared/Toast';
import { diagramApi } from './diagramApi';
import { DiagramType, DiagramTypeLabels } from './types';
import type { DiagramDetail } from './types';
import { translateUmlError } from './translateUmlError';

const examplesByType: Record<DiagramType, { title: string; json: string }> = {
  [DiagramType.UseCase]: {
    title: 'CampusEats Order Management',
    json: JSON.stringify({
      type: 'UseCase',
      title: 'CampusEats Order Management',
      systemBoundary: 'CampusEats',
      actors: ['Student', 'Restaurant'],
      useCases: ['Order Food', 'Track Order', 'Update Menu'],
      relations: [
        { from: 'Student', to: 'Order Food', type: 'association' },
        { from: 'Student', to: 'Track Order', type: 'association' },
        { from: 'Restaurant', to: 'Update Menu', type: 'association' },
      ],
    }, null, 2),
  },
  [DiagramType.BusinessContext]: {
    title: 'E-Commerce System Context',
    json: JSON.stringify({
      type: 'BusinessContext',
      title: 'E-Commerce System Context',
      system: 'Order Fulfillment Service',
      internalComponents: ['Order API', 'Inventory Worker', 'Payment Gateway Proxy'],
      externalSystems: ['Customer App', 'Payment Gateway', 'Logistics Partner'],
      relations: [
        { from: 'Customer App', to: 'Order API', protocol: 'HTTPS' },
        { from: 'Order API', to: 'Payment Gateway Proxy', protocol: 'gRPC' },
      ],
    }, null, 2),
  },
  [DiagramType.Sequence]: {
    title: 'Course Registration Flow',
    json: JSON.stringify({
      type: 'Sequence',
      title: 'Course Registration Flow',
      participants: ['Student', 'Registration API'],
      messages: [
        { from: 'Student', to: 'Registration API', label: 'register(courseId)', isResponse: false },
        { from: 'Registration API', to: 'Student', label: 'success(202)', isResponse: true },
      ],
    }, null, 2),
  },
  [DiagramType.Activity]: {
    title: 'Student Registration Workflow',
    json: JSON.stringify({
      type: 'Activity',
      title: 'Student Registration Workflow',
      startNode: 'Open Registration Portal',
      steps: ['Enter Course Code', 'Validate Prerequisites', 'Enroll Student'],
      decisions: [
        { condition: 'Prerequisites met?', yesBranch: 'Enroll Student', noBranch: 'Display Error' },
      ],
    }, null, 2),
  },
  [DiagramType.State]: {
    title: 'Render Job Lifecycle',
    json: JSON.stringify({
      type: 'State',
      title: 'Render Job Lifecycle',
      initialState: 'Draft',
      states: ['Draft', 'Pending', 'Rendering', 'Rendered', 'Failed'],
      transitions: [
        { from: 'Draft', event: 'submit', to: 'Pending' },
        { from: 'Pending', event: 'worker_dequeue', to: 'Rendering' },
        { from: 'Rendering', event: 'render_success', to: 'Rendered' },
      ],
    }, null, 2),
  },
};

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
  const [type, setType] = useState<DiagramType>(DiagramType.UseCase);
  const [title, setTitle] = useState(examplesByType[DiagramType.UseCase].title);
  const [modelJson, setModelJson] = useState(examplesByType[DiagramType.UseCase].json);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [errorField, setErrorField] = useState<'title' | 'model' | null>(null);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  useEffect(() => { if (error) errorElement.current?.focus(); }, [error]);

  function handleTypeChange(newType: DiagramType) {
    setType(newType);
    const ex = examplesByType[newType];
    if (ex) {
      setTitle(ex.title);
      setModelJson(ex.json);
    }
  }

  function handleLoadExample() {
    const ex = examplesByType[type];
    if (ex) {
      setTitle(ex.title);
      setModelJson(ex.json);
    }
  }

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
    let payloadModel = modelJson;
    try {
      const parsed = JSON.parse(modelJson);
      if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
        if (!parsed.title || !String(parsed.title).trim()) {
          parsed.title = title.trim();
        }
        if (!parsed.type) {
          parsed.type = DiagramType[type] || 'UseCase';
        }
        payloadModel = JSON.stringify(parsed, null, 2);
      }
    } catch {
      setErrorField('model');
      setError('Model JSON chưa đúng cú pháp. Kiểm tra dấu ngoặc, dấu phẩy và dấu nháy.');
      return;
    }
    pending.current = true;
    setBusy(true);
    try {
      const created = await diagramApi.createDiagram({ projectId, type, title: title.trim(), modelJson: payloadModel });
      if (!mounted.current) return;
      notify('Đã tạo sơ đồ.');
      onClose();
      onCreated(created);
    } catch (error: unknown) {
      if (!mounted.current) return;
      if (error instanceof ApiError) {
        const flattened = flattenErrors(error);
        if (flattened.length > 0) {
          const translatedList = Array.from(new Set(flattened.map(translateUmlError)));
          setError(`Mô hình sơ đồ vi phạm quy tắc UML:\n${translatedList.map(e => `• ${e}`).join('\n')}`);
        } else {
          setError(translateUmlError(error.message));
        }
      } else {
        setError(error instanceof Error ? translateUmlError(error.message) : 'Không tạo được sơ đồ. Vui lòng thử lại.');
      }
    } finally {
      pending.current = false;
      if (mounted.current) setBusy(false);
    }
  }

  return <Modal title="Tạo sơ đồ UML mới" onClose={onClose} busy={busy}>
    <form onSubmit={submit} aria-busy={busy} aria-describedby={error ? `${id}-error` : undefined}>
      {error && <p ref={errorElement} id={`${id}-error`} className="notice danger" role="alert" tabIndex={-1} style={{ whiteSpace: 'pre-line' }}>{error}</p>}
      <fieldset disabled={busy}>
        <label htmlFor={`${id}-title`}>Tiêu đề sơ đồ</label>
        <input id={`${id}-title`} data-autofocus required value={title} onChange={event => setTitle(event.target.value)}
          aria-invalid={errorField === 'title'} aria-describedby={error ? `${id}-error` : undefined} />
        <label htmlFor={`${id}-type`}>Loại sơ đồ</label>
        <select id={`${id}-type`} className="input" value={type} onChange={event => handleTypeChange(Number(event.target.value) as DiagramType)}>
          {Object.entries(DiagramTypeLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
        </select>
        <label htmlFor={`${id}-model`}>Model JSON</label>
        <textarea id={`${id}-model`} className="code-textarea" rows={8} required value={modelJson} onChange={event => setModelJson(event.target.value)}
          aria-invalid={errorField === 'model'} aria-describedby={error ? `${id}-error` : undefined} />
        <div style={{ display: 'flex', gap: '8px', marginTop: '6px' }}>
          <button type="button" className="dialog-example" onClick={handleLoadExample}>
            📋 Nạp ví dụ mẫu chuẩn (Hợp lệ)
          </button>
        </div>
      </fieldset>
      <div className="dialog-actions">
        <button type="button" className="button button-secondary" disabled={busy} onClick={onClose}>Hủy</button>
        <button type="submit" className="button button-primary" disabled={busy}>{busy ? 'Đang tạo…' : 'Tạo sơ đồ'}</button>
      </div>
    </form>
  </Modal>;
}
