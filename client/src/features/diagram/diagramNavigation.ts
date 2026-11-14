/** Chỉ quay lại danh sách nội bộ của đúng project; link mở trực tiếp mặc định về tab Sơ đồ. */
export function diagramListLink(projectId: string | undefined, returnTo?: unknown): string {
  if (!projectId) return '/projects';
  const projectPath = `/projects/${encodeURIComponent(projectId)}`;
  const fallback = `${projectPath}?tab=diagrams`;
  if (typeof returnTo !== 'string') return fallback;
  try {
    const url = new URL(returnTo, 'http://umlforge.local');
    if (url.origin !== 'http://umlforge.local') return fallback;
    if (url.pathname === projectPath || url.pathname === `${projectPath}/diagrams`
      || url.pathname === '/diagrams' && url.searchParams.get('projectId') === projectId) {
      return url.pathname + url.search + url.hash;
    }
  } catch { /* Ngữ cảnh không hợp lệ dùng tab Sơ đồ của project hiện tại. */ }
  return fallback;
}
