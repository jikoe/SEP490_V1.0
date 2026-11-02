import { esc, cleanMessage, money, localTime } from './page-ui.js';
export { esc, cleanMessage, money, localTime };
export const statuses = {
  Pending: 'Chờ xác minh', AwaitingPayment: 'Chờ xác minh', Processing: 'Đang xử lý',
  Succeeded: 'Thành công', Success: 'Thành công', Paid: 'Đã thanh toán', Completed: 'Hoàn thành',
  Failed: 'Thất bại', Expired: 'Hết hạn', Cancelled: 'Đã hủy', Active: 'Đang hoạt động',
  PendingPayment: 'Chờ thanh toán', Confirmed: 'Đã xác nhận', Issued: 'Đã phát hành',
  Approved: 'Đã duyệt', PartiallyRefunded: 'Hoàn một phần', Refunded: 'Đã hoàn tiền',
  Delivered: 'Đã giao hàng', Received: 'Đã nhận hàng', Shipping: 'Đang giao hàng',
  Preparing: 'Đang chuẩn bị', PendingConfirmation: 'Chờ xác nhận', Draft: 'Bản nháp',
  Requested: 'Khởi tạo yêu cầu', Mismatch: 'Có sai lệch', LatePayment: 'Thanh toán sau khi hủy',
  NeedsReview: 'Cần kiểm tra', VerificationFailed: 'Chưa xác minh được', AlreadyApplied: 'Đã ghi nhận',
};
export const metrics = [
  ['Gmv', 'GMV', 'Đã loại đơn hủy và khoản hoàn tiền hoàn tất'],
  ['Commission', 'Doanh thu hoa hồng', 'Hoa hồng của các kỳ quyết toán đã chi trả'],
  ['Subscriptions', 'Doanh thu gói thành viên', 'Khoản thu gói B2B đã xác minh'],
  ['Refunds', 'Hoàn tiền đã xác minh', 'Chỉ tính giao dịch hoàn tiền thành công'],
  ['PendingPayouts', 'Chờ thanh toán nông hộ', 'Tách riêng khoản đang chờ / đang xử lý'],
];
export function status(value, label) {
  const kind = ['Succeeded', 'Success', 'Paid', 'Completed', 'Active'].includes(value) ? 'success'
    : ['Failed', 'Cancelled', 'Expired'].includes(value) ? 'danger' : 'pending';
  return `<span class="fin-status ${kind}">${esc(label || statuses[value] || 'Cần kiểm tra')}</span>`;
}
export const dateLabel = value => value ? value.split('-').reverse().join('/') : '—';
export function todayVietnam() {
  const parts = Object.fromEntries(new Intl.DateTimeFormat('en', { timeZone: 'Asia/Ho_Chi_Minh',
    year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date()).map(p => [p.type, p.value]));
  return `${parts.year}-${parts.month}-${parts.day}`;
}
export function paymentUrl(id) {
  const params = new URLSearchParams(location.search);
  params.set('id', id);
  return `payment-detail.html?${params}`;
}
export function paymentTable(items, compact = false) {
  return `<div class="fin-table-wrap"><table class="fin-table"><thead><tr><th>Mã giao dịch / ${compact ? 'Nghĩa vụ liên kết' : 'Tham chiếu cổng'}</th>
    ${!compact ? '<th>Loại / Nghĩa vụ liên kết</th>' : ''}<th>Cổng thanh toán</th><th class="amount">Số tiền</th>
    ${!compact ? '<th>Tạo lúc / Thanh toán lúc</th>' : ''}<th>Trạng thái</th><th>Thao tác</th></tr></thead><tbody>${items.map(row => `<tr>
    <td><strong>${esc(row.code)}</strong><small>${esc(compact ? row.obligation : row.reference || 'Chưa có tham chiếu')}</small></td>
    ${!compact ? `<td>${row.category === 'Order' ? 'Đơn hàng' : 'Gói thành viên'}<small>${esc(row.obligation)}</small></td>` : ''}
    <td>${esc(row.provider)}</td><td class="amount">${money(row.amount)}</td>
    ${!compact ? `<td>${esc(localTime(row.requestedAt))}<small>${row.paidAt ? `Đã trả: ${esc(localTime(row.paidAt))}` : 'Chưa xác minh thời gian trả'}</small></td>` : ''}
    <td>${status(row.status)}${row.needsReview ? `<small class="fin-discrepancy">${esc(cleanMessage(row.note) || 'Có sai lệch cần kiểm tra')}</small>` : ''}${row.cancelledOrder ? '<small>Đơn hàng đã hủy</small>' : ''}</td>
    <td><a class="fin-view" href="${esc(paymentUrl(row.id))}">Xem chi tiết</a></td></tr>`).join('')}</tbody></table></div>`;
}
export function facts(values) {
  return `<dl class="fin-facts">${values.map(([label, value]) => `<div><dt>${label}</dt><dd>${value}</dd></div>`).join('')}</dl>`;
}
export function panel(title, body, caption = '') {
  return `<section class="fin-panel"><div class="fin-panel-heading"><h2>${title}</h2><span>${caption}</span></div>${body}</section>`;
}
export function match(expected, actual, amount = false) {
  if (actual == null) return '<span class="fin-muted">Chưa có kết quả</span>';
  return `<span class="fin-status ${expected === actual ? 'success' : 'pending'}">${expected === actual ? 'Khớp' : amount ? `Lệch ${money(Math.abs(expected - actual))}` : 'Cần kiểm tra'}</span>`;
}
