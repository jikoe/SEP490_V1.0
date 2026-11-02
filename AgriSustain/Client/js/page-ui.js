export const esc = value => Utils.escapeHtml(value ?? '');
export function cleanMessage(value) {
  return String(value ?? '').replace(/\b(?:MSG\d+|UC-[A-Z]+-\d+)\b/gi, '')
    .replace(/\(\s*\)/g, '').replace(/^\s*[-:·–]+\s*/, '').trim();
}
export const money = value => value == null ? 'Không khả dụng' : Utils.formatVND(value);
export function localTime(value) {
  if (!value) return '—';
  const date = new Date(/[zZ]$|[+-]\d{2}:\d{2}$/.test(value) ? value : `${value}Z`);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit',
  });
}
export function safeUrl(value) {
  if (!value) return '';
  try {
    const url = new URL(value, location.origin);
    return ['http:', 'https:'].includes(url.protocol) ? url.href : '';
  } catch { return ''; }
}
const paths = {
  leaf: 'M20 3C9 2 3 7 5 15c7 3 14-2 15-12ZM3 21 16 8M9 15l-1-5m1 5 5 1',
  shield: 'm12 2 9 4v6c0 6-9 10-9 10S3 18 3 12V6Zm-5 9 4 4 6-7',
  cart: 'M2 3h3l3 12h11l3-9H6M9 21h.01M19 21h.01',
  user: 'M20 21a8 8 0 0 0-16 0M17 7a5 5 0 1 1-10 0 5 5 0 0 1 10 0',
  grid: 'M3 3h6v6H3ZM15 3h6v6h-6ZM3 15h6v6H3ZM15 15h6v6h-6Z',
  card: 'M3 5h18v14H3ZM3 9h18M6 15h4',
  truck: 'M1 4h14v12H1ZM15 8h4l4 5v3h-8M5 21a3 3 0 1 0 0-6 3 3 0 0 0 0 6M19 21a3 3 0 1 0 0-6 3 3 0 0 0 0 6',
  check: 'm4 12 5 5L20 6', share: 'M18 5 6 12l12 7M21 5a3 3 0 1 1-6 0 3 3 0 0 1 6 0M9 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0M21 19a3 3 0 1 1-6 0 3 3 0 0 1 6 0',
  search: 'm21 21-5-5M18 10a8 8 0 1 1-16 0 8 8 0 0 1 16 0',
};
export const icon = (name = 'leaf') => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="${paths[name] || paths.leaf}"/></svg>`;
export function productImage(url, name) {
  const source = safeUrl(url);
  return source ? `<img src="${esc(source)}" alt="${esc(name)}" loading="lazy">`
    : `<div class="mkt-image-fallback" role="img" aria-label="${esc(name)}">${icon()}<span>Chưa có ảnh sản phẩm</span></div>`;
}
export async function request(path, options = {}) {
  const result = await API.request(path, { suppressErrorToast: true, ...options });
  if (!result.success) {
    const error = new Error(cleanMessage(result.error || 'Không thể tải dữ liệu. Vui lòng thử lại.'));
    error.status = result.status;
    error.fields = result.data?.errors || {};
    throw error;
  }
  return result.data;
}
export function showNotice(element, value, error = true) {
  element.textContent = cleanMessage(value);
  element.hidden = !value;
  element.className = `notice${error ? ' error' : ''}`;
  element.setAttribute('role', error ? 'alert' : 'status');
}
