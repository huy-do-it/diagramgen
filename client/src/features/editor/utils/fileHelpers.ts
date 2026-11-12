// Chép từ client v1 (utils/exportFiles.ts) — chỉ giữ phần editor cần; ZIP/PNG thuộc trang xuất (T-06)
const INVALID_FILE_NAME_CHARS = /[\\/:*?"<>|\u0000-\u001f]/g;

export function errorMessage(error: unknown): string {
  return error instanceof Error && error.message ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.';
}

// API trả DateTime UTC; chuỗi thiếu 'Z' thì thêm vào để không bị hiểu là giờ địa phương
export function formatUtcDateTime(value: string): string {
  const date = new Date(/(Z|[+-]\d{2}:\d{2})$/i.test(value) ? value : `${value}Z`);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString('vi-VN');
}

export function safeFileName(name: string, fallback: string): string {
  const cleaned = name.replace(INVALID_FILE_NAME_CHARS, '_').trim().replace(/\.+$/, '').slice(0, 100).trim();
  return cleaned.length > 0 ? cleaned : fallback;
}

export function svgTextToBlob(svgText: string): Blob {
  return new Blob([svgText], { type: 'image/svg+xml;charset=utf-8' });
}

export function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  // Trì hoãn revoke để trình duyệt kịp bắt đầu tải file
  window.setTimeout(() => URL.revokeObjectURL(url), 1_000);
}
