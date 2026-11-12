export function RenderError({ message, retry }: { message: string; retry?: () => void }) {
  return <div className="render-error" role="alert">{message}{retry && <button type="button" onClick={retry}>Thử lại</button>}</div>;
}
export function RenderPager({ page, pages, total, busy, change }: { page: number; pages: number; total: number; busy: boolean; change: (page: number) => void }) {
  return <nav className="render-pagination" aria-label="Phân trang"><span>Tổng {total} mục</span><div><button type="button" className="button button-secondary" disabled={busy || page <= 1} onClick={() => change(page - 1)}>Trước</button><span>{page} / {Math.max(1, pages)}</span><button type="button" className="button button-secondary" disabled={busy || page >= pages} onClick={() => change(page + 1)}>Sau</button></div></nav>;
}
export function renderTime(value: string | null) {
  if (!value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  const parts = new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false,
  }).formatToParts(date);
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find(x => x.type === type)?.value ?? '';
  return `${part('day')}/${part('month')}/${part('year')} ${part('hour')}:${part('minute')}`;
}
export const errorMessage = (e: unknown) => e instanceof Error ? e.message : 'Không thực hiện được yêu cầu. Vui lòng thử lại.';
