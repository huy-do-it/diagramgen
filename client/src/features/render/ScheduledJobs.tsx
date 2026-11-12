import { useEffect, useRef, useState } from 'react';
import { listScheduledJobs, runScheduledJob, serviceLabels, serviceNames } from './renderApi';
import type { JobRunResult, ScheduledJob, ServiceName } from './renderApi';
import { errorMessage, RenderError, renderTime } from './RenderUi';
const jobLabels: Record<string, string> = { reconcile: 'Đối soát job kẹt', cleanup: 'Dọn dữ liệu render', 'daily-render-report': 'Lập báo cáo ngày', 'purge-read-notifications': 'Dọn thông báo đã đọc', 'expire-invitations': 'Hết hạn lời mời', 'purge-refresh-tokens': 'Dọn refresh token' };
function ServiceJobs({ service, onSuccess }: { service: ServiceName; onSuccess: (message: string) => void }) {
  const [jobs, setJobs] = useState<ScheduledJob[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [revision, setRevision] = useState(0);
  const [busy, setBusy] = useState<string | null>(null);
  const [result, setResult] = useState<JobRunResult | null>(null);
  const [actionError, setActionError] = useState('');
  const mutation = useRef<AbortController | null>(null);
  useEffect(() => () => mutation.current?.abort(), []);
  useEffect(() => {
    const controller = new AbortController(); setLoading(true); setError(''); setJobs(null);
    listScheduledJobs(service, controller.signal).then(data => { if (!controller.signal.aborted) setJobs(data); })
      .catch((e: unknown) => { if (!controller.signal.aborted) setError(errorMessage(e)); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [service, revision]);
  async function run(name: string) {
    if (busy) return;
    const controller = new AbortController(); mutation.current = controller; setBusy(name); setResult(null); setActionError('');
    try {
      const value = await runScheduledJob(service, name, controller.signal);
      if (!controller.signal.aborted) { setResult(value); setRevision(x => x + 1); if (value.succeeded) onSuccess(`${serviceLabels[service]}: job đã chạy xong.`); }
    } catch (e) { if (!controller.signal.aborted) setActionError(`${errorMessage(e)} Chưa xác nhận được kết quả; hãy làm mới trạng thái trước khi chạy lại.`); }
    finally { if (!controller.signal.aborted) setBusy(null); }
  }
  return <section className="render-service-card" aria-label={`Job ${serviceLabels[service]}`}><div className="render-panel-heading"><h3>{serviceLabels[service]}</h3><button type="button" className="render-text-button" disabled={loading || !!busy} onClick={() => setRevision(x => x + 1)}>Tải lại</button></div>
    {result && <div className={result.succeeded ? 'render-success' : 'render-error'} role={result.succeeded ? undefined : 'alert'}><strong>{result.succeeded ? 'Đã chạy xong' : 'Job không hoàn tất'}</strong><p>{result.summary}</p><span>{renderTime(result.finishedAt)} · {result.durationMs.toLocaleString('vi-VN')} ms</span></div>}
    {actionError && <RenderError message={actionError} />}
    {error && <RenderError message={`Chưa tải được job của service này. ${error}`} retry={() => setRevision(x => x + 1)} />}
    {loading ? <div className="render-skeleton" role="status">Đang tải job định kỳ…</div> : jobs && jobs.length === 0 ? <p className="render-empty">Service chưa đăng ký job định kỳ.</p> : jobs && <ul className="render-scheduled-list">{jobs.map(job => <li key={job.jobName}><div><strong>{jobLabels[job.jobName] ?? job.jobName}</strong><p className="muted">Chu kỳ: {intervalLabel(job.interval)}</p>{job.lastResult && <span className={job.lastResult.succeeded ? 'render-last-success' : 'render-last-error'}>{job.lastResult.succeeded ? 'Lần trước hoàn tất' : 'Lần trước thất bại'} · {renderTime(job.lastResult.finishedAt)}</span>}</div><button type="button" className="button button-secondary" disabled={!!busy} onClick={() => void run(job.jobName)}>{busy === job.jobName ? 'Đang chạy…' : 'Chạy ngay'}</button></li>)}</ul>}
  </section>;
}
function intervalLabel(value: string) {
  const match = /^(?:(\d+)\.)?(\d{2}):(\d{2}):(\d{2})(?:\.\d+)?$/.exec(value);
  if (!match) return value;
  const [, days, hours, minutes, seconds] = match;
  return [[Number(days ?? 0), 'ngày'], [Number(hours), 'giờ'], [Number(minutes), 'phút'], [Number(seconds), 'giây']]
    .filter(([count]) => Number(count) > 0).map(([count, label]) => `${count} ${label}`).join(' ') || '0 giây';
}
export function ScheduledJobs({ onSuccess }: { onSuccess: (message: string) => void }) {
  return <section className="render-panel" aria-labelledby="scheduled-title"><h2 id="scheduled-title">Job định kỳ</h2><p className="muted">Chạy thủ công khi cần đối soát, dọn dữ liệu hoặc lập báo cáo.</p><div className="render-services">{serviceNames.map(service => <ServiceJobs key={service} service={service} onSuccess={onSuccess} />)}</div></section>;
}
