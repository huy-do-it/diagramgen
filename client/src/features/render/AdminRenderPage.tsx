import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router';
import { useAuth } from '../../auth/AuthContext';
import { ApiError } from '../../shared/api';
import type { PagedResult } from '../../shared/api';
import { listJobs, listReports, retryJob } from './renderApi';
import type { DailyReport, JobStatus, RenderJob } from './renderApi';
import { errorMessage, RenderError, RenderPager, renderTime } from './RenderUi';
import { ScheduledJobs } from './ScheduledJobs';
import './render.css';
const labels: Record<JobStatus, string> = { Queued: 'Chờ render', Running: 'Đang render', Done: 'Hoàn tất', Failed: 'Thất bại' };
function RenderJobs({ onSuccess }: { onSuccess: (message: string) => void }) {
  const [status, setStatus] = useState<JobStatus | ''>('');
  const [page, setPage] = useState(1);
  const [revision, setRevision] = useState(0);
  const [data, setData] = useState<PagedResult<RenderJob> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [actionError, setActionError] = useState('');
  const [retrying, setRetrying] = useState<string | null>(null);
  const mutation = useRef<AbortController | null>(null);
  useEffect(() => () => mutation.current?.abort(), []);
  useEffect(() => {
    const controller = new AbortController(); setLoading(true); setError(''); setData(null);
    listJobs(page, status, controller.signal).then(result => {
      if (controller.signal.aborted) return;
      if (page > Math.max(1, result.totalPages)) { setPage(Math.max(1, result.totalPages)); return; }
      setData(result);
    }).catch((e: unknown) => { if (!controller.signal.aborted) setError(errorMessage(e)); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [page, status, revision]);
  async function retry(id: string) {
    if (retrying) return;
    const controller = new AbortController(); mutation.current = controller; setRetrying(id); setActionError('');
    try { await retryJob(id, controller.signal); if (!controller.signal.aborted) { onSuccess('Đã xếp lại job vào hàng chờ.'); setRevision(x => x + 1); } }
    catch (e) { if (!controller.signal.aborted) { setActionError(errorMessage(e)); if (e instanceof ApiError && (e.status === 409 || e.status === 404)) setRevision(x => x + 1); } }
    finally { if (!controller.signal.aborted) setRetrying(null); }
  }
  return <section className="render-panel" aria-labelledby="render-jobs-title"><div className="render-panel-heading"><h2 id="render-jobs-title">Công việc render</h2><button type="button" className="button button-secondary" disabled={loading || !!retrying} onClick={() => setRevision(x => x + 1)}>Làm mới jobs</button></div>
    <label className="render-filter">Trạng thái <select value={status} disabled={!!retrying} onChange={e => { setStatus(e.target.value as JobStatus | ''); setPage(1); setActionError(''); }}><option value="">Tất cả</option>{Object.entries(labels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
    {actionError && <RenderError message={actionError} />}{error && <RenderError message={error} retry={() => setRevision(x => x + 1)} />}
    {loading ? <div className="render-skeleton" role="status">Đang tải jobs…</div> : data && data.items.length === 0 ? <div className="render-empty">Không có job phù hợp.</div> : data && <div className="render-table-scroll"><table><caption className="render-sr-only">Danh sách công việc render</caption><thead><tr><th>Job / Diagram</th><th>Phiên bản</th><th>Trạng thái</th><th>Lần chạy</th><th>Tạo lúc</th><th>Thời lượng</th><th>Thao tác</th></tr></thead><tbody>{data.items.map(job => <tr key={job.id}><td><span className="render-id" title={job.id}>{job.id.slice(0, 8)}</span><Link to={`/diagrams/${encodeURIComponent(job.diagramId)}`}>Mở diagram</Link></td><td>{job.diagramVersion}</td><td><span className={`render-badge render-status-${job.status}`}>{labels[job.status] ?? job.status}</span>{job.error && <details><summary>Chi tiết lỗi</summary><p>{job.error}</p></details>}</td><td>{job.attempt}</td><td>{renderTime(job.createdAt)}</td><td>{job.durationMs === null ? '—' : `${job.durationMs.toLocaleString('vi-VN')} ms`}</td><td>{job.status === 'Failed' ? <button type="button" className="button button-secondary" disabled={!!retrying} onClick={() => void retry(job.id)}>{retrying === job.id ? 'Đang xếp lại…' : 'Retry'}</button> : '—'}</td></tr>)}</tbody></table></div>}
    {data && data.totalItems > 0 && <RenderPager page={page} pages={data.totalPages} total={data.totalItems} busy={loading || !!retrying} change={setPage} />}
  </section>;
}
function RenderReports() {
  const [page, setPage] = useState(1);
  const [revision, setRevision] = useState(0);
  const [data, setData] = useState<PagedResult<DailyReport> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  useEffect(() => {
    const controller = new AbortController(); setLoading(true); setError(''); setData(null);
    listReports(page, controller.signal).then(result => {
      if (controller.signal.aborted) return;
      if (page > Math.max(1, result.totalPages)) { setPage(Math.max(1, result.totalPages)); return; }
      setData(result);
    }).catch((e: unknown) => { if (!controller.signal.aborted) setError(errorMessage(e)); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [page, revision]);
  return <section className="render-panel" aria-labelledby="render-reports-title"><div className="render-panel-heading"><h2 id="render-reports-title">Báo cáo ngày</h2><button type="button" className="button button-secondary" disabled={loading} onClick={() => setRevision(x => x + 1)}>Làm mới báo cáo</button></div><p className="muted">Ngày báo cáo theo UTC. Ngày còn job chưa kết thúc sẽ được chốt sau.</p>
    {error && <RenderError message={error} retry={() => setRevision(x => x + 1)} />}
    {loading ? <div className="render-skeleton" role="status">Đang tải báo cáo…</div> : data && data.items.length === 0 ? <div className="render-empty">Chưa có báo cáo ngày.</div> : data && <div className="render-table-scroll"><table><caption className="render-sr-only">Báo cáo render hằng ngày</caption><thead><tr><th>Ngày UTC</th><th>Tổng</th><th>Hoàn tất</th><th>Thất bại</th><th>Trung bình</th><th>Thành công</th></tr></thead><tbody>{data.items.map(report => <tr key={report.date}><td>{report.date.split('-').reverse().join('/')}</td><td>{report.total}</td><td>{report.done}</td><td>{report.failed}</td><td>{report.avgMs.toLocaleString('vi-VN', { maximumFractionDigits: 1 })} ms</td><td>{report.successRate.toLocaleString('vi-VN', { maximumFractionDigits: 1 })}%</td></tr>)}</tbody></table></div>}
    {data && data.totalItems > 0 && <RenderPager page={page} pages={data.totalPages} total={data.totalItems} busy={loading} change={setPage} />}
  </section>;
}
function AdminConsole() {
  const [toast, setToast] = useState<{ message: string } | null>(null);
  const showToast = (message: string) => setToast({ message });
  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(null), 4000);
    return () => window.clearTimeout(timer);
  }, [toast]);
  return <div className="minh-render"><header><p className="eyebrow">VẬN HÀNH HỆ THỐNG</p><h1>Giám sát render</h1><p className="muted">Theo dõi công việc, báo cáo và kết quả các job định kỳ.</p></header>
    <RenderJobs onSuccess={showToast} /><RenderReports /><ScheduledJobs onSuccess={showToast} />
    {toast && <p className="render-success render-toast" role="status" aria-live="polite">{toast.message}</p>}
  </div>;
}
export function AdminRenderPage() {
  const { user } = useAuth();
  if (!user || user.role !== 'Admin') return <section className="minh-render"><h1>Không có quyền truy cập</h1><p role="alert">Trang giám sát render chỉ dành cho Admin.</p></section>;
  return <AdminConsole key={user.id} />;
}
