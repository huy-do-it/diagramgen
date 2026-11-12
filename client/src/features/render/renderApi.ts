import { apiRequest } from '../../shared/api';
import type { PagedResult } from '../../shared/api';
export type JobStatus = 'Queued' | 'Running' | 'Done' | 'Failed';
export interface RenderJob { id: string; diagramId: string; diagramVersion: number; status: JobStatus; attempt: number; error: string | null; createdAt: string; durationMs: number | null }
export interface DailyReport { date: string; total: number; done: number; failed: number; avgMs: number; successRate: number }
export interface JobRunResult { jobName: string; succeeded: boolean; summary: string; durationMs: number; finishedAt: string; trigger: string }
export interface ScheduledJob { jobName: string; interval: string; lastResult: JobRunResult | null }
export const serviceNames = ['identity', 'workspace', 'diagram', 'render', 'notification'] as const;
export type ServiceName = typeof serviceNames[number];
export const serviceLabels: Record<ServiceName, string> = { identity: 'Tài khoản', workspace: 'Dự án', diagram: 'Sơ đồ', render: 'Render', notification: 'Thông báo' };
export const listJobs = (page: number, status: JobStatus | '', signal: AbortSignal) => apiRequest<PagedResult<RenderJob>>(`/render/jobs?page=${page}&pageSize=20${status ? `&status=${status}` : ''}`, { signal });
export const retryJob = (id: string, signal: AbortSignal) => apiRequest<{ jobId: string }>(`/render/jobs/${encodeURIComponent(id)}/retry`, { method: 'POST', signal });
export const listReports = (page: number, signal: AbortSignal) => apiRequest<PagedResult<DailyReport>>(`/render/reports?page=${page}&pageSize=20`, { signal });
export const listScheduledJobs = (service: ServiceName, signal: AbortSignal) => apiRequest<ScheduledJob[]>(`/admin/${service}/jobs`, { signal });
export const runScheduledJob = (service: ServiceName, name: string, signal: AbortSignal) => apiRequest<JobRunResult>(`/admin/${service}/jobs/${encodeURIComponent(name)}/run`, { method: 'POST', signal });
