import { apiRequest } from '../../shared/api';
import type { PagedResult } from '../../shared/api';

export interface NotificationItem {
  id: string; type: string; title: string; link: string; isRead: boolean;
  readAt: string | null; createdAt: string;
}
export const listNotifications = (page: number, unreadOnly: boolean, signal: AbortSignal, pageSize = 20) =>
  apiRequest<PagedResult<NotificationItem>>(`/notifications?page=${page}&pageSize=${pageSize}&unreadOnly=${unreadOnly}`, { signal });
export const unreadCount = (signal: AbortSignal) => apiRequest<{ count: number }>('/notifications/unread-count', { signal });
export const markRead = (id: string, signal: AbortSignal) => apiRequest<{ isRead: boolean }>(`/notifications/${encodeURIComponent(id)}/read`, { method: 'POST', signal });
export const markAllRead = (signal: AbortSignal) => apiRequest<{ updated: number }>('/notifications/read-all', { method: 'POST', signal });

const changed = 'umlforge:notification-read';
export function notifyRead(userId: string) { window.dispatchEvent(new CustomEvent(changed, { detail: userId })); }
export function onRead(userId: string, refresh: () => void) {
  const handler = (event: Event) => { if ((event as CustomEvent<string>).detail === userId) refresh(); };
  window.addEventListener(changed, handler);
  return () => window.removeEventListener(changed, handler);
}
// Chỉ cho phép link nội bộ đã quy ước, không điều hướng URL tùy ý từ payload.
export function notificationLink(link: string) {
  return /^\/(?:diagrams|projects)(?:\/[0-9a-f-]{36})?(?:\?tab=review)?$/i.test(link) ? link : '/notifications';
}
export function notificationTime(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  const parts = new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false,
  }).formatToParts(date);
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find(x => x.type === type)?.value ?? '';
  return `${part('day')}/${part('month')}/${part('year')} ${part('hour')}:${part('minute')}`;
}
