import React, { useCallback, useEffect, useId, useRef, useState } from 'react';
import { useLocation, useNavigate } from 'react-router';
import { ApiError, flattenErrors } from '../../shared/api';
import { useToast } from '../../shared/Toast';
import { diagramApi } from './diagramApi';
import type { DiagramDetail } from './types';
import { translateUmlError } from './translateUmlError';

interface DiagramEditPageProps {
  diagramId: string;
  onBack?: () => void;
}

import { DiagramType } from './types';

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

export const DiagramEditPage: React.FC<DiagramEditPageProps> = ({ diagramId, onBack }) => {
  const id = useId();
  const notify = useToast();
  const navigate = useNavigate();
  const location = useLocation();
  const mounted = useRef(false);
  const pending = useRef(false);

  const [diagram, setDiagram] = useState<DiagramDetail | null>(null);
  const [etag, setEtag] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState<string | null>(null);

  const [title, setTitle] = useState('');
  const [modelJson, setModelJson] = useState('');
  const [busy, setBusy] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saveErrorField, setSaveErrorField] = useState<'title' | 'model' | null>(null);

  const errorElement = useRef<HTMLParagraphElement>(null);

  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);

  useEffect(() => {
    if (saveError) errorElement.current?.focus();
  }, [saveError]);

  const fetchDiagram = useCallback(async () => {
    setLoading(true);
    setFetchError(null);
    try {
      const { diagram: data, etag: tag } = await diagramApi.getDiagramById(diagramId);
      if (!mounted.current) return;
      setDiagram(data);
      setEtag(tag);
      setTitle(data.title);
      setModelJson(
        (() => {
          try {
            return JSON.stringify(JSON.parse(data.modelJson), null, 2);
          } catch {
            return data.modelJson;
          }
        })()
      );
    } catch (err: unknown) {
      if (!mounted.current) return;
      setFetchError(err instanceof Error ? err.message : 'Không tải được sơ đồ.');
    } finally {
      if (mounted.current) setLoading(false);
    }
  }, [diagramId]);

  useEffect(() => {
    fetchDiagram();
  }, [fetchDiagram]);

  const isLocked =
    diagram?.status === 'Pending' || diagram?.status === 'Rendering';

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    if (pending.current || isLocked) return;
    setSaveError(null);
    setSaveErrorField(null);
    let payloadModel = modelJson;

    if (!title.trim()) {
      setSaveErrorField('title');
      setSaveError('Nhập tiêu đề sơ đồ.');
      return;
    }
    try {
      const parsed = JSON.parse(modelJson);
      if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
        if (!parsed.title || !String(parsed.title).trim()) {
          parsed.title = title.trim();
        }
        if (!parsed.type && diagram) {
          parsed.type = diagram.type;
        }
        payloadModel = JSON.stringify(parsed, null, 2);
      }
    } catch {
      setSaveErrorField('model');
      setSaveError('Model JSON chưa đúng cú pháp. Kiểm tra dấu ngoặc, dấu phẩy và dấu nháy.');
      return;
    }

    pending.current = true;
    setBusy(true);
    try {
      await diagramApi.updateDiagram(
        diagramId,
        { title: title.trim(), modelJson: payloadModel },
        etag ?? undefined
      );
      if (!mounted.current) return;
      notify('Đã lưu thay đổi sơ đồ.');
      navigate(`/diagrams/${diagramId}${location.search}`, { state: location.state });
    } catch (err: unknown) {
      if (!mounted.current) return;
      if (err instanceof ApiError && err.status === 412) {
        setSaveError(
          'Sơ đồ đã được chỉnh sửa bởi người khác. Vui lòng tải lại trang và thử lại.'
        );
      } else if (err instanceof ApiError && err.status === 409) {
        setSaveError('Không thể chỉnh sửa sơ đồ khi đang chờ render hoặc đang render.');
      } else {
        if (err instanceof ApiError) {
          const flattened = flattenErrors(err);
          if (flattened.length > 0) {
            const translatedList = Array.from(new Set(flattened.map(translateUmlError)));
            setSaveError(`Mô hình sơ đồ vi phạm quy tắc UML:\n${translatedList.map(e => `• ${e}`).join('\n')}`);
          } else {
            setSaveError(translateUmlError(err.message));
          }
        } else {
          setSaveError(
            err instanceof Error
              ? translateUmlError(err.message)
              : 'Không lưu được sơ đồ. Vui lòng thử lại.'
          );
        }
      }
    } finally {
      pending.current = false;
      if (mounted.current) setBusy(false);
    }
  }

  function handleBack() {
    if (onBack) {
      onBack();
    } else {
      navigate(`/diagrams/${diagramId}${location.search}`, { state: location.state });
    }
  }

  // ── Loading / Error states ──────────────────────────────────────────
  if (loading) {
    return (
      <div className="panel skeleton" style={{ height: '400px' }}>
        Đang tải sơ đồ...
      </div>
    );
  }

  if (fetchError || !diagram) {
    return (
      <div className="notice danger" role="alert">
        <p>{fetchError || 'Không tìm thấy sơ đồ.'}</p>
        <button className="button button-secondary" onClick={fetchDiagram}>
          Thử lại
        </button>
      </div>
    );
  }

  // ── Main render ─────────────────────────────────────────────────────
  return (
    <div className="diagram-edit-page" style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
      {/* Header */}
      <div className="panel" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <button
            className="button button-ghost button-small"
            onClick={handleBack}
            style={{ marginBottom: '8px' }}
          >
            ← Quay lại xem sơ đồ
          </button>
          <h1>Chỉnh sửa sơ đồ: {diagram.title}</h1>
          <div style={{ fontSize: '13px', color: 'var(--color-muted)', marginTop: '4px' }}>
            Phiên bản hiện tại: <strong>v{diagram.currentVersion}</strong> &nbsp;·&nbsp; Trạng thái:{' '}
            <strong>{diagram.status}</strong>
          </div>
        </div>
      </div>

      {/* Locked notice */}
      {isLocked && (
        <div className="notice warning" role="alert">
          <strong>⚠ Không thể chỉnh sửa:</strong> Sơ đồ đang ở trạng thái{' '}
          <strong>{diagram.status === 'Pending' ? 'Chờ render' : 'Đang render'}</strong>. Vui lòng đợi render hoàn tất rồi thử lại.
        </div>
      )}

      {/* Edit form */}
      <div className="panel">
        <form onSubmit={handleSubmit} aria-busy={busy} aria-describedby={saveError ? `${id}-error` : undefined}>
          {saveError && (
            <p
              ref={errorElement}
              id={`${id}-error`}
              className="notice danger"
              role="alert"
              tabIndex={-1}
              style={{ whiteSpace: 'pre-line' }}
            >
              {saveError}
            </p>
          )}

          <fieldset disabled={busy || isLocked}>
            {/* Title */}
            <label htmlFor={`${id}-title`}>Tiêu đề sơ đồ</label>
            <input
              id={`${id}-title`}
              required
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              aria-invalid={saveErrorField === 'title'}
              aria-describedby={saveError ? `${id}-error` : undefined}
              placeholder="Nhập tiêu đề sơ đồ..."
            />

            {/* Model JSON */}
            <label htmlFor={`${id}-model`} style={{ marginTop: '16px', display: 'block' }}>
              Model JSON
            </label>
            <textarea
              id={`${id}-model`}
              className="code-textarea"
              rows={16}
              required
              value={modelJson}
              onChange={(e) => setModelJson(e.target.value)}
              aria-invalid={saveErrorField === 'model'}
              aria-describedby={saveError ? `${id}-error` : undefined}
              placeholder='{ "systemBoundary": "...", "actors": [...], ... }'
              style={{ fontFamily: 'monospace', fontSize: '13px' }}
            />

            {/* Example helpers */}
            <div style={{ display: 'flex', gap: '8px', marginTop: '6px' }}>
              <button
                type="button"
                className="dialog-example"
                onClick={() => {
                  if (diagram) {
                    const ex = examplesByType[diagram.type];
                    if (ex) {
                      setTitle(ex.title);
                      setModelJson(ex.json);
                    }
                  }
                }}
              >
                📋 Nạp ví dụ mẫu chuẩn (Hợp lệ)
              </button>
            </div>

            {/* Info box */}
            <div
              style={{
                marginTop: '12px',
                padding: '10px 14px',
                background: '#f0f7ff',
                border: '1px solid #bfdbfe',
                borderRadius: '6px',
                fontSize: '13px',
                color: '#1e40af',
              }}
            >
              <strong>ℹ Lưu ý:</strong> Lưu thay đổi sẽ tạo phiên bản mới (v{diagram.currentVersion + 1}) và
              chuyển trạng thái sơ đồ về <em>Nháp</em>. Bạn cần render lại để tạo hình ảnh mới.
            </div>
          </fieldset>

          {/* Actions */}
          <div
            className="dialog-actions"
            style={{ marginTop: '20px', display: 'flex', gap: '8px', justifyContent: 'flex-end' }}
          >
            <button
              type="button"
              className="button button-secondary"
              disabled={busy}
              onClick={handleBack}
            >
              Hủy
            </button>
            <button
              type="submit"
              className="button button-primary"
              disabled={busy || isLocked}
            >
              {busy ? 'Đang lưu…' : '💾 Lưu thay đổi'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
