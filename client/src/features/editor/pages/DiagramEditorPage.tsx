import { useEffect, useId, useMemo, useRef, useState } from 'react';
import type { KeyboardEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { useAuth } from '../../../auth/AuthContext';
import { ApiError } from '../../../shared/api';
import { isRenderActive, useRenderStatus } from '../../../shared/useRenderStatus';
import type { RenderSnapshot, RenderStatus } from '../../../shared/useRenderStatus';
import { DiagramExamplePanel } from '../components/DiagramExamplePanel';
import {
  createDiagram,
  deleteDiagram,
  getDiagram,
  getSuggestions,
  requestRender,
  toRenderSnapshot,
  toTypeEnum,
  toTypeName,
  updateDiagram,
  validateDiagram,
} from '../services/diagramService';
import type { DiagramDetail, RelationSuggestion, ValidationResult } from '../services/diagramService';
import { DIAGRAM_TEMPLATES, DIAGRAM_TYPE_OPTIONS, DiagramTypeEnum, getDiagramTemplate } from '../templates/diagramTemplates';
import { findBlanks, findNextBlank, syncJsonTitle } from '../utils/diagramJson';
import { downloadBlob, errorMessage, formatUtcDateTime, safeFileName, svgTextToBlob } from '../utils/fileHelpers';

interface JsonStatus {
  valid: boolean;
  message?: string;
  line?: number;
}

// Badge trạng thái theo DESIGN §2.4 (luôn có chữ + icon, không chỉ dùng màu)
const STATUS_BADGES: Record<RenderStatus, { label: string; icon: string; className: string }> = {
  Draft: { label: 'Nháp', icon: '✎', className: '' },
  Pending: { label: 'Chờ render', icon: '⏳', className: 'status-pending' },
  Rendering: { label: 'Đang render…', icon: '⟳', className: 'status-pending' },
  Rendered: { label: 'Đã render', icon: '✓', className: 'status-rendered' },
  Failed: { label: 'Render lỗi', icon: '✕', className: 'status-failed' },
};

const TYPE_LABELS = Object.fromEntries(DIAGRAM_TYPE_OPTIONS.map(option => [option.value, option.label])) as Record<number, string>;

/** UI-06: trình soạn sơ đồ (JSON + khung điền chỗ trống + ví dụ + preview). Chuyển từ DiagramEditorPage v1. */
export function DiagramEditorPage() {
  const { projectId, diagramId } = useParams();
  const navigate = useNavigate();
  const { user } = useAuth();
  const isCreate = !diagramId;
  const isLecturer = user?.role === 'Lecturer';

  const [title, setTitle] = useState('');
  const [type, setType] = useState<DiagramTypeEnum>(DiagramTypeEnum.UseCase);
  const [modelJson, setModelJson] = useState('');

  const [diagram, setDiagram] = useState<DiagramDetail | null>(null);
  const [etag, setEtag] = useState<string | null>(null);
  const [loadedSnapshot, setLoadedSnapshot] = useState<RenderSnapshot | null>(null);
  const render = useRenderStatus(diagramId ?? null, loadedSnapshot);

  const [loading, setLoading] = useState(!isCreate);
  const [saving, setSaving] = useState(false);
  const [sendingRender, setSendingRender] = useState(false);
  const [activeTab, setActiveTab] = useState<'preview' | 'plantuml' | 'example'>(isCreate ? 'example' : 'preview');
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  const [validation, setValidation] = useState<ValidationResult | null>(null);
  const [suggestions, setSuggestions] = useState<RelationSuggestion[] | null>(null);
  const [checking, setChecking] = useState(false);
  const [copiedUml, setCopiedUml] = useState(false);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const titleId = useId();
  const typeId = useId();
  const jsonId = useId();

  const status: RenderStatus = render.status ?? diagram?.status ?? 'Draft';
  const renderActive = isRenderActive(status);
  // BR-13: không sửa khi đang Pending/Rendering · BR-32: Lecturer chỉ xem
  const canEdit = !isLecturer && !renderActive;
  const isDirty = isCreate || (diagram !== null && (diagram.title !== title.trim() || diagram.modelJson !== modelJson.trim()));

  function applyLoaded(next: DiagramDetail, nextEtag: string | null) {
    setDiagram(next);
    setEtag(nextEtag);
    setTitle(next.title);
    setType(toTypeEnum(next.type));
    setModelJson(next.modelJson);
    setLoadedSnapshot(toRenderSnapshot(next, nextEtag));
  }

  // Tạo mới: nạp khung điền chỗ trống của loại đang chọn
  useEffect(() => {
    if (isCreate && !modelJson) setModelJson(syncJsonTitle(getDiagramTemplate(type).skeleton, title));
    // Chỉ chạy khi vào trang tạo mới
  }, [isCreate]);

  // Sửa: tải sơ đồ + ETag
  useEffect(() => {
    if (isCreate || !diagramId) return;
    const controller = new AbortController();
    setLoading(true);
    setError(null);
    getDiagram(diagramId, controller.signal)
      .then(({ diagram: loaded, etag: loadedEtag }) => {
        applyLoaded(loaded, loadedEtag);
        setLoading(false);
      })
      .catch((failure: unknown) => {
        if (controller.signal.aborted) return;
        setError(errorMessage(failure));
        setLoading(false);
      });
    return () => controller.abort();
  }, [isCreate, diagramId]);

  // Consumer đổi trạng thái làm RowVersion đổi → luôn dùng ETag mới nhất cho lần lưu sau
  useEffect(() => {
    if (render.snapshot?.etag) setEtag(render.snapshot.etag);
  }, [render.snapshot?.etag]);

  // Báo kết quả khi render vừa kết thúc
  const previousStatus = useRef<RenderStatus | null>(null);
  useEffect(() => {
    const before = previousStatus.current;
    previousStatus.current = status;
    if (!isRenderActive(before)) return;
    if (status === 'Rendered') {
      setNotice(`Đã render xong phiên bản ${render.snapshot?.version ?? ''}.`);
      setActiveTab('preview');
    } else if (status === 'Failed') {
      setError(`Render lỗi: ${render.error ?? 'không rõ nguyên nhân'}. Ảnh cũ (nếu có) vẫn được giữ.`);
    }
  }, [status]);

  const jsonStatus: JsonStatus = useMemo(() => {
    if (!modelJson.trim()) return { valid: false, message: 'JSON không được để trống.' };
    try {
      const parsed: unknown = JSON.parse(modelJson);
      if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) {
        return { valid: false, message: 'JSON gốc phải là object ({ ... }).' };
      }
      return { valid: true };
    } catch (failure) {
      const message = failure instanceof Error ? failure.message : String(failure);
      const match = /line\s+(\d+)/i.exec(message);
      return { valid: false, message, line: match ? Number(match[1]) : undefined };
    }
  }, [modelJson]);

  const blanks = useMemo(() => findBlanks(modelJson), [modelJson]);

  const svgPreviewUrl = useMemo(() => render.svg ? URL.createObjectURL(svgTextToBlob(render.svg)) : null, [render.svg]);
  useEffect(() => () => { if (svgPreviewUrl) URL.revokeObjectURL(svgPreviewUrl); }, [svgPreviewUrl]);

  function clearMessages() {
    setNotice(null);
    setError(null);
    setConflict(false);
  }

  // "title" trong JSON đi theo ô Tên sơ đồ để không phải nhập 2 lần
  function handleTitleChange(value: string) {
    setTitle(value);
    setModelJson(current => syncJsonTitle(current, value));
  }

  // Bôi đen chỗ trống <...> kế tiếp để người dùng gõ đè lên
  function handleNextBlank() {
    const textarea = textareaRef.current;
    if (!textarea) return;
    const next = findNextBlank(modelJson, textarea.selectionEnd);
    if (!next) return;
    textarea.focus();
    textarea.setSelectionRange(next.start, next.end);
  }

  function handleTextareaKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key !== 'Tab') return;
    event.preventDefault();
    const target = event.currentTarget;
    const { selectionStart: start, selectionEnd: end, value } = target;
    setModelJson(value.substring(0, start) + '  ' + value.substring(end));
    window.requestAnimationFrame(() => { target.selectionStart = target.selectionEnd = start + 2; });
  }

  function handleFormatJson() {
    try {
      setModelJson(JSON.stringify(JSON.parse(modelJson), null, 2));
      setNotice('Đã định dạng lại JSON.');
    } catch {
      setError('JSON đang sai cú pháp, sửa lỗi trước khi định dạng.');
    }
  }

  // JSON chưa bị sửa nếu (bỏ qua "title") trùng khung trống hoặc ví dụ của 1 loại sơ đồ
  function isPristineTemplate(json: string): boolean {
    const normalized = syncJsonTitle(json, '').trim();
    return Object.values(DIAGRAM_TEMPLATES).some(
      template => template.skeleton.trim() === normalized || syncJsonTitle(template.example.json, '').trim() === normalized);
  }

  // Đổi loại sơ đồ chỉ khi tạo mới (sơ đồ đã lưu giữ nguyên loại)
  function handleSelectType(newType: DiagramTypeEnum) {
    if (modelJson.trim() && !isPristineTemplate(modelJson)
      && !window.confirm('Thay nội dung editor bằng khung của loại sơ đồ mới? Thay đổi chưa lưu sẽ mất.')) {
      return;
    }
    setType(newType);
    setModelJson(syncJsonTitle(getDiagramTemplate(newType).skeleton, title));
    setValidation(null);
  }

  function handleUseExample() {
    const template = getDiagramTemplate(type);
    if (modelJson.trim() && !isPristineTemplate(modelJson)
      && !window.confirm('Thay nội dung editor bằng ví dụ? Thay đổi chưa lưu sẽ mất.')) {
      return;
    }
    const isExampleTitle = Object.values(DIAGRAM_TEMPLATES).some(t => t.example.title === title.trim());
    const nextTitle = title.trim() && !isExampleTitle ? title : template.example.title;
    setTitle(nextTitle);
    setModelJson(syncJsonTitle(template.example.json, nextTitle));
    setValidation(null);
  }

  function checkBeforeSubmit(): boolean {
    if (!title.trim()) {
      setError('Cần nhập tên sơ đồ.');
      return false;
    }
    if (!jsonStatus.valid) {
      setError(`JSON chưa hợp lệ: ${jsonStatus.message}`);
      return false;
    }
    if (blanks.length > 0) {
      setError(`Còn ${blanks.length} chỗ trống <...> chưa điền.`);
      return false;
    }
    return true;
  }

  function handleApiFailure(failure: unknown) {
    if (failure instanceof ApiError && failure.status === 422) {
      // Lỗi luật UML từ gRPC ValidateDiagram (T-05)
      const errors = Object.values(failure.errors).flat();
      setValidation({ isValid: false, errors: errors.length ? errors : [failure.message], warnings: [] });
      setError('Sơ đồ chưa đúng luật UML — xem danh sách lỗi dưới editor.');
    } else if (failure instanceof ApiError && failure.status === 409 && failure.code === 'BR-16') {
      setConflict(true);
      setError('Sơ đồ vừa được người khác lưu. Tải lại để xem bản mới nhất trước khi sửa tiếp.');
    } else {
      setError(errorMessage(failure));
    }
  }

  /** Lưu (tạo mới hoặc PUT kèm If-Match). Trả id sơ đồ nếu thành công. */
  async function save(askBeforeDraft: boolean): Promise<string | null> {
    if (!checkBeforeSubmit()) return null;
    if (askBeforeDraft && (status === 'Rendered' || status === 'Failed')
      && !window.confirm('Lưu sẽ đưa sơ đồ về Nháp và tạo phiên bản mới. Ảnh hiện tại vẫn được giữ để xem cho tới khi render lại. Tiếp tục?')) {
      return null;
    }

    setSaving(true);
    clearMessages();
    try {
      if (isCreate) {
        if (!projectId) {
          setError('Thiếu dự án cho sơ đồ mới.');
          return null;
        }
        const { diagram: created } = await createDiagram({
          projectId, type: toTypeName(type), title: title.trim(), modelJson: modelJson.trim(),
        });
        return created.id;
      }

      const { diagram: updated, etag: newEtag } = await updateDiagram(diagramId!, { title: title.trim(), modelJson: modelJson.trim() }, etag);
      applyLoaded(updated, newEtag);
      setValidation(updated.warnings?.length ? { isValid: true, errors: [], warnings: updated.warnings } : null);
      setNotice(updated.needsResubmit
        ? `Đã lưu phiên bản ${updated.currentVersion}. Dự án đã được duyệt trước đó — cần gửi duyệt lại.`
        : `Đã lưu phiên bản ${updated.currentVersion}.`);
      return updated.id;
    } catch (failure) {
      handleApiFailure(failure);
      return null;
    } finally {
      setSaving(false);
    }
  }

  async function handleSave() {
    const savedId = await save(true);
    if (savedId && isCreate) navigate(`/diagrams/${savedId}/edit`, { replace: true });
  }

  // Lưu nếu có thay đổi rồi gửi yêu cầu render; trạng thái tự cập nhật qua useRenderStatus
  async function handleRender() {
    let targetId = diagramId ?? null;
    if (isDirty && canEdit) {
      targetId = await save(false);
      if (!targetId) return;
    }
    if (!targetId) return;

    setSendingRender(true);
    try {
      const accepted = await requestRender(targetId);
      if (isCreate) {
        navigate(`/diagrams/${targetId}/edit`, { replace: true });
        return;
      }
      render.track();
      setNotice(`Đã gửi yêu cầu render phiên bản ${accepted.version}. Sơ đồ đang render, bạn có thể tiếp tục làm việc.`);
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 409 && failure.code === 'BR-14') {
        setError('Sơ đồ đang chờ hoặc đang render, chưa gửi thêm được.');
        void render.refresh();
      } else {
        handleApiFailure(failure);
      }
      if (isCreate && targetId) navigate(`/diagrams/${targetId}/edit`, { replace: true });
    } finally {
      setSendingRender(false);
    }
  }

  // Validate nhận modelJson đang soạn (kể cả chưa lưu); Suggest của Diagram API vẫn chạy trên bản đã lưu
  const SAVE_BEFORE_SUGGEST = 'Còn thay đổi chưa lưu. Bấm "Lưu" trước — gợi ý quan hệ chạy trên bản đã lưu.';

  async function handleValidate() {
    if (!diagramId || !checkBeforeSubmit()) return;
    setChecking(true);
    clearMessages();
    try {
      const result = await validateDiagram(diagramId, modelJson.trim());
      setValidation(result);
      if (result.isValid) setNotice('Sơ đồ đạt luật UML.');
      else setError(`Có ${result.errors.length} lỗi UML.`);
    } catch (failure) {
      handleApiFailure(failure);
    } finally {
      setChecking(false);
    }
  }

  async function handleSuggest() {
    if (!diagramId) return;
    if (isDirty) {
      setError(SAVE_BEFORE_SUGGEST);
      return;
    }
    setChecking(true);
    clearMessages();
    try {
      setSuggestions(await getSuggestions(diagramId));
    } catch (failure) {
      handleApiFailure(failure);
    } finally {
      setChecking(false);
    }
  }

  async function handleReload() {
    if (!diagramId) return;
    if (isDirty && !window.confirm('Tải lại sẽ bỏ các thay đổi chưa lưu của bạn. Tiếp tục?')) return;
    clearMessages();
    try {
      const { diagram: latest, etag: latestEtag } = await getDiagram(diagramId);
      applyLoaded(latest, latestEtag);
      setNotice(`Đã tải bản mới nhất (phiên bản ${latest.currentVersion}).`);
    } catch (failure) {
      setError(errorMessage(failure));
    }
  }

  async function handleDelete() {
    if (!diagram || !window.confirm(`Xóa sơ đồ "${diagram.title}"?`)) return;
    try {
      await deleteDiagram(diagram.id);
      navigate(`/projects/${diagram.projectId}`);
    } catch (failure) {
      setError(errorMessage(failure));
    }
  }

  function handleDownloadSvg() {
    if (!render.svg) return;
    downloadBlob(svgTextToBlob(render.svg), `${safeFileName(title, 'diagram')}.svg`);
  }

  async function handleCopyPlantUml() {
    const plantUml = render.snapshot?.plantUml;
    if (!plantUml) return;
    try {
      await navigator.clipboard.writeText(plantUml);
      setCopiedUml(true);
      setTimeout(() => setCopiedUml(false), 2500);
    } catch {
      setError('Không copy được vào clipboard.');
    }
  }

  if (loading) {
    return <section className="panel"><p className="muted" role="status">Đang tải sơ đồ…</p></section>;
  }

  if (!isCreate && !diagram) {
    return <section className="panel">
      <div className="form-error" role="alert">{error ?? 'Không tải được sơ đồ.'}</div>
    </section>;
  }

  const badge = STATUS_BADGES[status];
  const backProjectId = diagram?.projectId ?? projectId;
  const plantUml = render.snapshot?.plantUml ?? null;

  return (
    <>
      <nav aria-label="Breadcrumb">
        <p className="eyebrow">
          {backProjectId
            ? <Link to={`/projects/${backProjectId}`} style={{ color: 'inherit', textDecoration: 'none' }}>DỰ ÁN</Link>
            : 'DỰ ÁN'}
          {' / '}
          {isCreate ? 'SƠ ĐỒ MỚI' : `SƠ ĐỒ · PHIÊN BẢN ${diagram?.currentVersion ?? ''}`}
        </p>
      </nav>

      <header className="editor-header">
        <div className="editor-header-title">
          <h1>{isCreate ? 'Tạo sơ đồ mới' : title || 'Sửa sơ đồ'}</h1>
          {!isCreate && (
            <span className={`status-badge ${badge.className}`} aria-label={`Trạng thái: ${badge.label}`}>
              {badge.icon} {badge.label}
            </span>
          )}
        </div>

        <div className="editor-header-actions">
          {!isCreate && (
            <Link to={`/diagrams/${diagramId}`} className="button button-secondary button-small">Xem sơ đồ</Link>
          )}
          {!isCreate && !isLecturer && (
            <button type="button" className="button button-danger button-small" onClick={handleDelete} disabled={renderActive}>
              Xóa
            </button>
          )}
          {!isLecturer && (
            <>
              <button type="button" className="button button-secondary" onClick={handleSave} disabled={saving || !canEdit || !isDirty}>
                {saving ? 'Đang lưu…' : isCreate ? 'Lưu nháp' : 'Lưu sơ đồ'}
              </button>
              <button type="button" className="button button-primary" onClick={handleRender}
                disabled={sendingRender || saving || renderActive}>
                {sendingRender ? 'Đang gửi…' : renderActive ? 'Đang render…' : 'Render'}
              </button>
            </>
          )}
        </div>
      </header>

      {notice && <div className="notice" role="status" style={{ marginBottom: 16 }}>{notice}</div>}
      {error && (
        <div className="form-error" role="alert" style={{ marginBottom: 16 }}>
          {error}
          {conflict && (
            <button type="button" className="button button-secondary button-small" style={{ marginLeft: 12 }} onClick={handleReload}>
              Tải lại
            </button>
          )}
        </div>
      )}
      {render.pollError && <div className="notice" role="status" style={{ marginBottom: 16 }}>{render.pollError}</div>}

      {isLecturer && (
        <div className="notice" style={{ marginBottom: 20 }}>Giảng viên chỉ xem sơ đồ, không sửa (BR-32).</div>
      )}
      {!isCreate && renderActive && (
        <div className="notice" style={{ marginBottom: 20 }}>
          Sơ đồ đang render ({badge.label}). Tạm khóa chỉnh sửa tới khi render xong; trạng thái tự cập nhật.
        </div>
      )}

      <div className="editor-layout">
        <section className="panel" aria-label="Soạn model sơ đồ">
          <div className="panel-header">
            <h2>Model</h2>
            <span className="muted" style={{ fontSize: 13 }}>{TYPE_LABELS[type]}</span>
          </div>

          <div style={{ marginBottom: 16 }}>
            <label className="field-label" htmlFor={titleId}>Tên sơ đồ</label>
            <input id={titleId} type="text" value={title} onChange={event => handleTitleChange(event.target.value)}
              placeholder="Ví dụ: CampusEats - Use Case" disabled={!canEdit} style={{ width: '100%' }} />
          </div>

          <div style={{ marginBottom: 16 }}>
            <label className="field-label" htmlFor={typeId}>Loại sơ đồ</label>
            <select id={typeId} className="select" value={type}
              onChange={event => handleSelectType(Number(event.target.value) as DiagramTypeEnum)} disabled={!isCreate}>
              {DIAGRAM_TYPE_OPTIONS.map(option => (
                <option key={option.value} value={option.value}>{option.label} — {option.description}</option>
              ))}
            </select>
          </div>

          {isCreate && (
            <div style={{ marginBottom: 12 }}>
              <span className="field-label">Mẫu nhanh</span>
              <div className="template-chips">
                {DIAGRAM_TYPE_OPTIONS.map(option => (
                  <button key={option.value} type="button" className={`chip-btn ${type === option.value ? 'active' : ''}`}
                    onClick={() => handleSelectType(option.value)}>
                    {option.badge}
                  </button>
                ))}
              </div>
            </div>
          )}

          <div className="toolbar">
            <label className="field-label" htmlFor={jsonId} style={{ margin: 0 }}>Model JSON</label>
            <div className="toolbar-group">
              <button type="button" className="button button-secondary button-small" onClick={handleFormatJson} disabled={!canEdit}>
                Định dạng JSON
              </button>
              {!isCreate && (
                <>
                  <button type="button" className="button button-secondary button-small" onClick={handleValidate}
                    disabled={checking || !jsonStatus.valid}>
                    {checking ? 'Đang kiểm tra…' : 'Kiểm tra UML'}
                  </button>
                  <button type="button" className="button button-secondary button-small" onClick={handleSuggest} disabled={checking}>
                    Gợi ý quan hệ
                  </button>
                </>
              )}
            </div>
          </div>

          <textarea ref={textareaRef} id={jsonId} className={`code-textarea ${!jsonStatus.valid ? 'has-error' : ''}`}
            value={modelJson} onChange={event => setModelJson(event.target.value)} onKeyDown={handleTextareaKeyDown}
            disabled={!canEdit} spellCheck={false} aria-describedby={`${jsonId}-status`} />

          <div className="json-status-bar" id={`${jsonId}-status`}>
            {!jsonStatus.valid ? (
              <span className="json-invalid">
                ✕ JSON sai {jsonStatus.line ? `(quanh dòng ${jsonStatus.line})` : ''}: {jsonStatus.message}
              </span>
            ) : blanks.length > 0 ? (
              <span className="json-blanks">
                ✎ Còn {blanks.length} chỗ trống
                <button type="button" className="button button-secondary button-small" onClick={handleNextBlank} disabled={!canEdit}>
                  Chỗ trống tiếp ▸
                </button>
              </span>
            ) : (
              <span className="json-valid">✓ JSON hợp lệ</span>
            )}
            <span className="muted">{modelJson.length} ký tự</span>
          </div>

          {isCreate && (
            <p className="muted" style={{ fontSize: 12, marginTop: 8 }}>
              Luật UML được kiểm tra khi lưu; lỗi (nếu có) hiện ngay dưới đây.
            </p>
          )}

          {validation && (
            <div className={validation.isValid ? 'notice' : 'form-error'} style={{ marginTop: 14 }}>
              <strong>{validation.isValid ? '✓ Đạt luật UML' : '✕ Lỗi luật UML:'}</strong>
              {validation.errors.length > 0 && (
                <ul style={{ margin: '6px 0 0', paddingLeft: 20 }}>
                  {validation.errors.map(item => <li key={item} style={{ color: '#a12d3b' }}>{item}</li>)}
                </ul>
              )}
              {validation.warnings.length > 0 && (
                <ul style={{ margin: '6px 0 0', paddingLeft: 20 }}>
                  {validation.warnings.map(item => <li key={item} style={{ color: '#8a5a00' }}>Cảnh báo: {item}</li>)}
                </ul>
              )}
            </div>
          )}

          {suggestions && (
            <div className="notice" style={{ marginTop: 14 }}>
              <strong>{suggestions.length ? 'Gợi ý quan hệ:' : 'Không có gợi ý nào.'}</strong>
              {suggestions.length > 0 && (
                <ul style={{ margin: '6px 0 0', paddingLeft: 20 }}>
                  {suggestions.map(item => <li key={`${item.from}->${item.to}`}>{item.from} → {item.to}: {item.description}</li>)}
                </ul>
              )}
            </div>
          )}
        </section>

        <section className="panel preview-container" aria-label="Kết quả render">
          <div className="panel-header">
            <h2>Kết quả</h2>
            <div className="export-actions">
              {render.svg && (
                <button type="button" className="button button-secondary button-small" onClick={handleDownloadSvg}>Tải SVG</button>
              )}
            </div>
          </div>

          <div className="tabs" role="tablist">
            <button type="button" role="tab" aria-selected={activeTab === 'preview'}
              className={`tab-btn ${activeTab === 'preview' ? 'active' : ''}`} onClick={() => setActiveTab('preview')}>
              Xem SVG
            </button>
            <button type="button" role="tab" aria-selected={activeTab === 'plantuml'}
              className={`tab-btn ${activeTab === 'plantuml' ? 'active' : ''}`} onClick={() => setActiveTab('plantuml')}>
              Mã PlantUML
            </button>
            <button type="button" role="tab" aria-selected={activeTab === 'example'}
              className={`tab-btn ${activeTab === 'example' ? 'active' : ''}`} onClick={() => setActiveTab('example')}>
              Ví dụ & hướng dẫn
            </button>
          </div>

          {activeTab === 'preview' && (
            <div>
              {svgPreviewUrl ? (
                // Hiển thị SVG qua <img> (không chèn HTML) nên script trong SVG không chạy được
                <div className="svg-canvas" tabIndex={0}>
                  <img src={svgPreviewUrl} alt={title || 'Sơ đồ đã render'} role="img" />
                </div>
              ) : (
                <div className="empty-state" style={{ padding: '48px 20px', minHeight: 360 }}>
                  <span className="empty-icon" aria-hidden="true">❖</span>
                  <h2>Chưa có ảnh SVG</h2>
                  <p>
                    {renderActive ? 'Sơ đồ đang render, ảnh sẽ tự hiện khi xong.'
                      : status === 'Failed' ? 'Lần render gần nhất bị lỗi. Sửa model rồi bấm Render lại.'
                      : 'Bấm "Render" để sinh mã PlantUML và ảnh SVG.'}
                  </p>
                </div>
              )}
              {render.snapshot?.renderedAt && (
                <p className="muted" style={{ fontSize: 12, marginTop: 12 }}>
                  Render lần cuối: {formatUtcDateTime(render.snapshot.renderedAt)}
                </p>
              )}
            </div>
          )}

          {activeTab === 'plantuml' && (
            <div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 8 }}>
                <button type="button" className="button button-secondary button-small" onClick={handleCopyPlantUml} disabled={!plantUml}>
                  {copiedUml ? '✓ Đã copy' : 'Copy PlantUML'}
                </button>
              </div>
              {plantUml
                ? <textarea readOnly className="plantuml-code" value={plantUml} aria-label="Mã PlantUML" />
                : <div className="empty-state" style={{ padding: '40px 20px' }}><p className="muted">Mã PlantUML có sau khi render.</p></div>}
            </div>
          )}

          {activeTab === 'example' && (
            <DiagramExamplePanel typeLabel={TYPE_LABELS[type] ?? 'sơ đồ'} template={getDiagramTemplate(type)}
              canUseExample={canEdit} onUseExample={handleUseExample} />
          )}
        </section>
      </div>
    </>
  );
}
